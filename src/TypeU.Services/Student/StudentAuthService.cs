using System;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Core.Devices;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Services.Student;

/// <summary>
/// 学生签到登录结果。
/// </summary>
public sealed class LoginResult
{
    /// <summary>是否成功。</summary>
    public bool Success { get; init; }

    /// <summary>失败原因（成功时为 null）。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>教师端返回的会话起始时间戳（UTC 毫秒，可选）。</summary>
    public long ServerTimestampMs { get; init; }
}

/// <summary>
/// 学生端签到登录服务：发送登录请求 + 处理教师端 LoginAck + 设备绑定校验。
/// </summary>
public sealed class StudentAuthService
{
    private readonly TcpExamClient _client;
    private readonly DeviceFingerprintProvider _fingerprintProvider;
    private readonly ILogger<StudentAuthService>? _logger;
    private TaskCompletionSource<LoginResult>? _loginTcs;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    public StudentAuthService(
        TcpExamClient client,
        DeviceFingerprintProvider fingerprintProvider,
        ILogger<StudentAuthService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fingerprintProvider = fingerprintProvider ?? throw new ArgumentNullException(nameof(fingerprintProvider));
        _logger = logger;
        _client.PacketReceived += OnPacketReceived;
    }

    /// <summary>
    /// 发送签到登录请求，等待教师端 LoginAck 响应。
    /// </summary>
    /// <param name="studentId">学号。</param>
    /// <param name="name">姓名。</param>
    /// <param name="timeout">等待响应超时（默认 5 秒）。</param>
    /// <returns>登录结果。</returns>
    public async Task<LoginResult> LoginAsync(string studentId, string name, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new ArgumentException("学号不能为空。", nameof(studentId));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("姓名不能为空。", nameof(name));
        }
        if (!_client.IsConnected)
        {
            return new LoginResult { Success = false, ErrorMessage = "未连接到教师端。" };
        }

        var fingerprint = _fingerprintProvider.GetFingerprint();
        var dto = new LoginDto
        {
            StudentId = studentId,
            Name = name,
            DeviceFingerprint = fingerprint,
            ComputerName = Environment.MachineName
        };

        _loginTcs = new TaskCompletionSource<LoginResult>();
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        cts.Token.Register(() => _loginTcs.TrySetResult(new LoginResult { Success = false, ErrorMessage = "登录超时，未收到教师端响应。" }));

        byte[] payload;
        using (var ms = new System.IO.MemoryStream())
        {
            Serializer.Serialize(ms, dto);
            payload = ms.ToArray();
        }

        try
        {
            await _client.SendAsync(MessageType.Login, payload).ConfigureAwait(false);
            _logger?.LogInformation("已发送登录请求：{StudentId}/{Name}", studentId, name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "发送登录请求失败");
            return new LoginResult { Success = false, ErrorMessage = ex.Message };
        }

        return await _loginTcs.Task.ConfigureAwait(false);
    }

    private Task OnPacketReceived(MessageType type, byte[] payload)
    {
        if (type != MessageType.LoginAck || _loginTcs is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            using var ms = new System.IO.MemoryStream(payload);
            var ack = Serializer.Deserialize<LoginAckDto>(ms);
            _loginTcs.TrySetResult(new LoginResult
            {
                Success = ack.Success,
                ErrorMessage = string.IsNullOrEmpty(ack.ErrorMessage) ? null : ack.ErrorMessage,
                ServerTimestampMs = ack.ServerTimestampMs
            });
        }
        catch (Exception ex)
        {
            _loginTcs.TrySetResult(new LoginResult { Success = false, ErrorMessage = $"登录响应解析失败：{ex.Message}" });
        }
        return Task.CompletedTask;
    }
}
