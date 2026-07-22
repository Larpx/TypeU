using System;
using System.IO;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 教师端报文处理器：订阅 TcpExamServer.PacketReceived，处理学生登录、状态上报、成绩回传。
/// 将 Login/StatusReport/ResultSubmit 等报文分发给对应业务服务。
/// </summary>
public sealed class TeacherPacketHandler : IDisposable
{
    private readonly TcpExamServer _server;
    private readonly DeviceBindingService _deviceBinding;
    private readonly MonitoringService _monitoring;
    private readonly GradeService _gradeService;
    private readonly ILogger<TeacherPacketHandler>? _logger;
    private bool _disposed;

    /// <summary>
    /// 初始化报文处理器。
    /// </summary>
    /// <param name="server">TCP 服务端。</param>
    /// <param name="deviceBinding">设备绑定服务。</param>
    /// <param name="monitoring">监控看板服务。</param>
    /// <param name="gradeService">成绩管理服务。</param>
    /// <param name="logger">日志。</param>
    public TeacherPacketHandler(
        TcpExamServer server,
        DeviceBindingService deviceBinding,
        MonitoringService monitoring,
        GradeService gradeService,
        ILogger<TeacherPacketHandler>? logger = null)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _deviceBinding = deviceBinding ?? throw new ArgumentNullException(nameof(deviceBinding));
        _monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _logger = logger;

        _server.PacketReceived += OnPacketReceived;
        _server.ClientConnected += OnClientConnected;
        _server.ClientDisconnected += OnClientDisconnected;
    }

    private async Task OnPacketReceived(string clientId, MessageType type, byte[] payload)
    {
        switch (type)
        {
            case MessageType.Login:
                await HandleLoginAsync(clientId, payload).ConfigureAwait(false);
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
            _logger?.LogWarning(ex, "Login 报文反序列化失败（客户端 {ClientId}）", clientId);
            return;
        }

        _logger?.LogInformation("收到登录请求：{StudentId}/{Name} 来自 {ClientId}", dto.StudentId, dto.Name, clientId);

        // 检查设备绑定是否冲突。
        var (bound, boundFp) = _deviceBinding.IsBoundToOtherDevice(dto.StudentId, dto.DeviceFingerprint);
        if (bound)
        {
            _logger?.LogWarning("设备绑定冲突：{StudentId} 已绑定指纹 {Fingerprint}，当前请求 {CurrentFp}",
                dto.StudentId, boundFp, dto.DeviceFingerprint);

            var ack = new LoginAckDto
            {
                Success = false,
                ErrorMessage = "该学号已绑定其他设备，请联系教师解绑。",
                ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            await SendLoginAckAsync(clientId, ack).ConfigureAwait(false);
            return;
        }

        // 记录绑定。
        _deviceBinding.Bind(dto.StudentId, dto.Name, dto.DeviceFingerprint);

        // 注册到监控看板。
        var clientIp = !string.IsNullOrEmpty(dto.ClientIp) ? dto.ClientIp : clientId;
        _monitoring.RegisterStudent(dto.StudentId, dto.Name, clientIp);

        // 回复 LoginAck。
        var successAck = new LoginAckDto
        {
            Success = true,
            ErrorMessage = string.Empty,
            ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await SendLoginAckAsync(clientId, successAck).ConfigureAwait(false);
        _logger?.LogInformation("学生 {StudentId} 登录成功", dto.StudentId);
    }

    private async Task SendLoginAckAsync(string clientId, LoginAckDto ack)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, ack);
        await _server.SendAsync(clientId, MessageType.LoginAck, ms.ToArray()).ConfigureAwait(false);
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
            _logger?.LogWarning(ex, "StatusReport 报文反序列化失败（客户端 {ClientId}）", clientId);
            return;
        }

        _monitoring.UpdateProgress(dto.StudentId, dto.Speed, dto.Accuracy, dto.Progress, dto.AnomalyCount);
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
            _logger?.LogWarning(ex, "ResultSubmit 报文反序列化失败（客户端 {ClientId}）", clientId);
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
            SubmittedAt = dto.SubmittedAt
        };
        _gradeService.CollectRecord(record);
        _monitoring.MarkSubmitted(dto.StudentId);
        _logger?.LogInformation("收到成绩：{StudentId} 速度 {Speed} 正确率 {Accuracy}%",
            dto.StudentId, dto.Speed, dto.Accuracy);
    }

    private void OnClientConnected(string clientId, System.Net.IPEndPoint endpoint)
    {
        _logger?.LogInformation("客户端 {ClientId} 已连接（{Endpoint}）", clientId, endpoint);
    }

    private void OnClientDisconnected(string clientId)
    {
        _logger?.LogInformation("客户端 {ClientId} 已断开", clientId);
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