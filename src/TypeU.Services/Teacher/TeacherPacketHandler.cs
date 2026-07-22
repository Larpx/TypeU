using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 教师端报文处理：Hello/Login/Logout/状态/成绩。
/// </summary>
public sealed class TeacherPacketHandler : IDisposable
{
    private readonly TcpExamServer _server;
    private readonly DeviceBindingService _deviceBinding;
    private readonly MonitoringService _monitoring;
    private readonly GradeService _gradeService;
    private readonly TeacherExamService _examService;
    private readonly SessionLoginRepository _sessionLogins;
    private readonly ILogger<TeacherPacketHandler>? _logger;
    private readonly ConcurrentDictionary<string, string> _clientFingerprints = new();
    private bool _disposed;

    /// <summary>
    /// 初始化。
    /// </summary>
    public TeacherPacketHandler(
        TcpExamServer server,
        DeviceBindingService deviceBinding,
        MonitoringService monitoring,
        GradeService gradeService,
        TeacherExamService examService,
        SessionLoginRepository sessionLogins,
        ILogger<TeacherPacketHandler>? logger = null)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _deviceBinding = deviceBinding ?? throw new ArgumentNullException(nameof(deviceBinding));
        _monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _examService = examService ?? throw new ArgumentNullException(nameof(examService));
        _sessionLogins = sessionLogins ?? throw new ArgumentNullException(nameof(sessionLogins));
        _logger = logger;

        _server.PacketReceived += OnPacketReceived;
        _server.ClientConnected += OnClientConnected;
        _server.ClientDisconnected += OnClientDisconnected;
    }

    /// <summary>
    /// 允许指定学生登出并下发通知。
    /// </summary>
    public async Task AllowLogoutAsync(string studentId)
    {
        var session = _examService.CurrentSession;
        if (session is null)
        {
            // 已结束会话：仍可按最近 Running→Ended 处理；此处要求调用方传入会话时用 Current 或最后一次
            throw new InvalidOperationException("无当前会话，请在考试进行中或结束后对登录名单操作。");
        }

        _sessionLogins.SetLogoutAllowed(session.SessionId, studentId, true);
        _monitoring.SetLogoutAllowed(studentId, true);

        var dto = new LogoutAllowDto
        {
            StudentId = studentId,
            SessionId = session.SessionId,
            Allowed = true
        };
        await BroadcastProtoAsync(MessageType.LogoutAllow, dto).ConfigureAwait(false);
        _logger?.LogInformation("已允许学生 {StudentId} 登出", studentId);
    }

    /// <summary>
    /// 对指定会话允许登出（结束考试后仍可用）。
    /// </summary>
    public async Task AllowLogoutAsync(Guid sessionId, string studentId)
    {
        _sessionLogins.SetLogoutAllowed(sessionId, studentId, true);
        _monitoring.SetLogoutAllowed(studentId, true);
        var dto = new LogoutAllowDto
        {
            StudentId = studentId,
            SessionId = sessionId,
            Allowed = true
        };
        await BroadcastProtoAsync(MessageType.LogoutAllow, dto).ConfigureAwait(false);
    }

    private async Task OnPacketReceived(string clientId, MessageType type, byte[] payload)
    {
        switch (type)
        {
            case MessageType.Hello:
                await HandleHelloAsync(clientId, payload).ConfigureAwait(false);
                break;
            case MessageType.Login:
                await HandleLoginAsync(clientId, payload).ConfigureAwait(false);
                break;
            case MessageType.Logout:
                HandleLogout(clientId, payload);
                break;
            case MessageType.StatusReport:
                HandleStatusReport(clientId, payload);
                break;
            case MessageType.ResultSubmit:
                HandleResultSubmit(clientId, payload);
                break;
            default:
                break;
        }
    }

    private async Task HandleHelloAsync(string clientId, byte[] payload)
    {
        HelloDto? dto;
        try
        {
            using var ms = new MemoryStream(payload);
            dto = Serializer.Deserialize<HelloDto>(ms);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Hello 反序列化失败 {ClientId}", clientId);
            return;
        }

        _clientFingerprints[clientId] = dto.DeviceFingerprint;
        _monitoring.RegisterConnection(clientId, dto.DeviceFingerprint, dto.ClientIp, dto.ComputerName);

        var session = _examService.CurrentSession;
        var ack = new HelloAckDto
        {
            ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        if (session is { Status: ExamSessionStatus.Running })
        {
            ack.ExamRunning = true;
            ack.SessionId = session.SessionId;
            ack.MaxAttempts = session.MaxAttempts;
            ack.AllowPracticeAfterSubmit = session.AllowPracticeAfterSubmit;

            var login = _sessionLogins.GetByDevice(session.SessionId, dto.DeviceFingerprint);
            if (login is not null)
            {
                ack.AutoLogin = true;
                ack.StudentId = login.StudentId;
                ack.StudentName = login.Name;
                ack.LogoutAllowed = login.LogoutAllowed;
                _monitoring.MarkLoggedIn(clientId, login.StudentId, login.Name);
            }
        }

        await SendProtoAsync(clientId, MessageType.HelloAck, ack).ConfigureAwait(false);
    }

    private async Task HandleLoginAsync(string clientId, byte[] payload)
    {
        LoginDto? dto;
        try
        {
            using var ms = new MemoryStream(payload);
            dto = Serializer.Deserialize<LoginDto>(ms);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Login 反序列化失败 {ClientId}", clientId);
            return;
        }

        var session = _examService.CurrentSession;
        if (session is null || session.Status != ExamSessionStatus.Running)
        {
            await SendLoginAckAsync(clientId, new LoginAckDto
            {
                Success = false,
                ErrorMessage = "当前未开考，无法登录。",
                ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }).ConfigureAwait(false);
            return;
        }

        var (bound, boundFp) = _deviceBinding.IsBoundToOtherDevice(dto.StudentId, dto.DeviceFingerprint);
        if (bound)
        {
            await SendLoginAckAsync(clientId, new LoginAckDto
            {
                Success = false,
                ErrorMessage = "该学号已绑定其他设备，请联系教师解绑。",
                ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }).ConfigureAwait(false);
            return;
        }

        _deviceBinding.Bind(dto.StudentId, dto.Name, dto.DeviceFingerprint);
        _clientFingerprints[clientId] = dto.DeviceFingerprint;

        var login = new SessionLogin
        {
            SessionId = session.SessionId,
            DeviceFingerprint = dto.DeviceFingerprint,
            StudentId = dto.StudentId,
            Name = dto.Name,
            LoggedInAt = DateTime.UtcNow,
            LogoutAllowed = false
        };
        _sessionLogins.Upsert(login);

        var clientIp = !string.IsNullOrEmpty(dto.ClientIp) ? dto.ClientIp : clientId;
        _monitoring.MarkLoggedIn(clientId, dto.StudentId, dto.Name);
        if (!string.IsNullOrEmpty(clientIp))
        {
            // 刷新 IP
            _monitoring.RegisterConnection(clientId, dto.DeviceFingerprint, clientIp, dto.ComputerName);
            _monitoring.MarkLoggedIn(clientId, dto.StudentId, dto.Name);
        }

        await SendLoginAckAsync(clientId, new LoginAckDto
        {
            Success = true,
            LogoutLocked = true,
            SessionId = session.SessionId,
            MaxAttempts = session.MaxAttempts,
            AllowPracticeAfterSubmit = session.AllowPracticeAfterSubmit,
            ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }).ConfigureAwait(false);

        _logger?.LogInformation("学生登录成功 {StudentId} 会话 {SessionId}", dto.StudentId, session.SessionId);
    }

    private void HandleLogout(string clientId, byte[] payload)
    {
        LogoutDto? dto;
        try
        {
            using var ms = new MemoryStream(payload);
            dto = Serializer.Deserialize<LogoutDto>(ms);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Logout 反序列化失败");
            return;
        }

        var login = _sessionLogins.GetByDevice(dto.SessionId, dto.DeviceFingerprint);
        var session = _examService.CurrentSession;
        var allowed = login?.LogoutAllowed == true
                      || (session is not null && session.SessionId == dto.SessionId && session.Status == ExamSessionStatus.Ended);

        if (!allowed)
        {
            _logger?.LogWarning("拒绝登出 {StudentId}：未授权", dto.StudentId);
            return;
        }

        _sessionLogins.Delete(dto.SessionId, dto.DeviceFingerprint);
        _monitoring.UnregisterStudent(dto.StudentId);
        _logger?.LogInformation("学生已登出 {StudentId}", dto.StudentId);
    }

    private void HandleStatusReport(string clientId, byte[] payload)
    {
        StatusReportDto? dto;
        try
        {
            using var ms = new MemoryStream(payload);
            dto = Serializer.Deserialize<StatusReportDto>(ms);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "StatusReport 反序列化失败");
            return;
        }

        var key = !string.IsNullOrEmpty(dto.StudentId) ? clientId : clientId;
        _monitoring.UpdateProgress(
            key,
            dto.Speed,
            dto.Accuracy,
            dto.Progress,
            dto.AnomalyCount,
            dto.IsMinimized,
            dto.ClientMode);
    }

    private void HandleResultSubmit(string clientId, byte[] payload)
    {
        ExamResultDto? dto;
        try
        {
            using var ms = new MemoryStream(payload);
            dto = Serializer.Deserialize<ExamResultDto>(ms);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ResultSubmit 反序列化失败");
            return;
        }

        var record = new ExamRecord
        {
            RecordId = dto.RecordId,
            SessionId = dto.SessionId,
            StudentId = dto.StudentId,
            Speed = dto.Speed,
            Accuracy = dto.Accuracy,
            Anomalies = dto.Anomalies ?? string.Empty,
            SubmittedAt = dto.SubmittedAt,
            AttemptIndex = dto.AttemptIndex <= 0 ? 1 : dto.AttemptIndex
        };
        _gradeService.CollectRecord(record);
        _monitoring.MarkSubmitted(dto.StudentId);
        _logger?.LogInformation("成绩入库 {StudentId} 第{Attempt}次", dto.StudentId, record.AttemptIndex);
    }

    private async Task SendLoginAckAsync(string clientId, LoginAckDto ack)
        => await SendProtoAsync(clientId, MessageType.LoginAck, ack).ConfigureAwait(false);

    private async Task SendProtoAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        string clientId, MessageType type, T msg)
        where T : class
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, msg);
        await _server.SendAsync(clientId, type, ms.ToArray()).ConfigureAwait(false);
    }

    private async Task BroadcastProtoAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        MessageType type, T msg)
        where T : class
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, msg);
        await _server.BroadcastAsync(type, ms.ToArray()).ConfigureAwait(false);
    }

    private void OnClientConnected(string clientId, System.Net.IPEndPoint endpoint)
    {
        _logger?.LogInformation("客户端连接 {ClientId} {Endpoint}", clientId, endpoint);
        _monitoring.RegisterConnection(clientId, string.Empty, endpoint.Address.ToString(), string.Empty);
    }

    private void OnClientDisconnected(string clientId)
    {
        _logger?.LogInformation("客户端断开 {ClientId}", clientId);
        _monitoring.UnregisterConnection(clientId);
        _clientFingerprints.TryRemove(clientId, out _);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _server.PacketReceived -= OnPacketReceived;
        _server.ClientConnected -= OnClientConnected;
        _server.ClientDisconnected -= OnClientDisconnected;
    }
}
