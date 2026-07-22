using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.NetworkInformation;
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
/// 学生联网：配置 IP → Ping → TCP → Hello 握手。
/// </summary>
public sealed class StudentConnectionService : IDisposable
{
    private readonly TcpExamClient _client;
    private readonly DeviceFingerprintProvider _fingerprintProvider;
    private readonly ILogger<StudentConnectionService>? _logger;
    private TaskCompletionSource<HelloAckDto>? _helloTcs;

    /// <summary>
    /// 初始化。
    /// </summary>
    public StudentConnectionService(
        TcpExamClient client,
        DeviceFingerprintProvider fingerprintProvider,
        ILogger<StudentConnectionService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fingerprintProvider = fingerprintProvider ?? throw new ArgumentNullException(nameof(fingerprintProvider));
        _logger = logger;
        _client.PacketReceived += OnPacketReceived;
    }

    /// <summary>
    /// Ping 主机是否可达。
    /// </summary>
    public static bool PingHost(string host, int timeoutMs = 1000)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(host, timeoutMs);
            return reply?.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 连接并发送 Hello，等待 HelloAck。
    /// </summary>
    public async Task<HelloAckDto?> ConnectAndHelloAsync(string host, int port, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("教师端 IP 不能为空。", nameof(host));
        }

        await _client.ConnectAsync(host, port, autoReconnect: true).ConfigureAwait(false);

        _helloTcs = new TaskCompletionSource<HelloAckDto>();
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        cts.Token.Register(() => _helloTcs.TrySetResult(null!));

        var hello = new HelloDto
        {
            DeviceFingerprint = _fingerprintProvider.GetFingerprint(),
            ComputerName = Environment.MachineName,
            ClientIp = string.Empty
        };
        await _client.SendAsync(MessageType.Hello, Serialize(hello)).ConfigureAwait(false);
        _logger?.LogInformation("已发送 Hello 至 {Host}:{Port}", host, port);

        var ack = await _helloTcs.Task.ConfigureAwait(false);
        return ack;
    }

    /// <summary>
    /// 发送登出。
    /// </summary>
    public async Task LogoutAsync(LogoutDto dto)
    {
        await _client.SendAsync(MessageType.Logout, Serialize(dto)).ConfigureAwait(false);
    }

    private Task OnPacketReceived(MessageType type, byte[] payload)
    {
        if (type != MessageType.HelloAck || _helloTcs is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            using var ms = new MemoryStream(payload);
            var ack = Serializer.Deserialize<HelloAckDto>(ms);
            _helloTcs.TrySetResult(ack);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "HelloAck 解析失败");
            _helloTcs.TrySetResult(null!);
        }

        return Task.CompletedTask;
    }

    private static byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T msg)
        where T : class
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, msg);
        return ms.ToArray();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.PacketReceived -= OnPacketReceived;
    }
}
