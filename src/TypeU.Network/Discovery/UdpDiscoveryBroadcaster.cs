using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Network.Messages;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Network.Discovery;

/// <summary>
/// UDP 发现广播器（教师端使用）：每 5 秒向子网广播自身 IP+TCP 端口。
/// </summary>
public sealed class UdpDiscoveryBroadcaster : IDisposable
{
    private readonly PacketCodec _codec;
    private readonly ILogger<UdpDiscoveryBroadcaster>? _logger;
    private readonly int _broadcastPort;
    private readonly TimeSpan _interval;
    private readonly IPAddress _targetAddress;
    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private UdpClient? _udp;

    /// <summary>
    /// 初始化广播器。
    /// </summary>
    /// <param name="codec">报文编解码器（广播包同样走加密+签名）。</param>
    /// <param name="broadcastPort">子网广播端口（学生端监听端口）。</param>
    /// <param name="logger">日志。</param>
    /// <param name="interval">广播间隔（默认 5 秒）。</param>
    /// <param name="targetAddress">目标地址（默认 IPAddress.Broadcast；测试可传 IPAddress.Loopback）。</param>
    public UdpDiscoveryBroadcaster(
        PacketCodec codec,
        int broadcastPort,
        ILogger<UdpDiscoveryBroadcaster>? logger = null,
        TimeSpan? interval = null,
        IPAddress? targetAddress = null)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _broadcastPort = broadcastPort;
        _logger = logger;
        _interval = interval ?? TimeSpan.FromSeconds(5);
        _targetAddress = targetAddress ?? IPAddress.Broadcast;
    }

    /// <summary>
    /// 启动定时广播。
    /// </summary>
    /// <param name="teacherPort">教师端 TCP 端口。</param>
    /// <param name="teacherName">教师端标识。</param>
    public void Start(int teacherPort, string teacherName)
    {
        if (_cts is not null)
        {
            throw new InvalidOperationException("广播器已启动。");
        }

        _cts = new CancellationTokenSource();
        _udp = new UdpClient();
        _udp.EnableBroadcast = true;
        _broadcastTask = Task.Run(() => BroadcastLoopAsync(teacherPort, teacherName, _cts.Token), _cts.Token);
        _logger?.LogInformation("UDP 广播器已启动，目标端口 {Port}", _broadcastPort);
    }

    /// <summary>
    /// 停止广播。
    /// </summary>
    public void Stop()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        _udp?.Dispose();
        try
        {
            _broadcastTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 忽略取消异常。
        }

        _cts.Dispose();
        _cts = null;
        _udp = null;
        _broadcastTask = null;
        _logger?.LogInformation("UDP 广播器已停止");
    }

    private async Task BroadcastLoopAsync(int teacherPort, string teacherName, CancellationToken ct)
    {
        var endpoint = new IPEndPoint(_targetAddress, _broadcastPort);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var msg = new DiscoveryBroadcastMessage
                {
                    TeacherPort = teacherPort,
                    TeacherName = teacherName,
                    TimestampMs = TimestampValidator.NowUtcMs()
                };

                using var ms = new System.IO.MemoryStream();
                Serializer.Serialize(ms, msg);
                var payload = ms.ToArray();

                var bytes = _codec.Encode(MessageType.DiscoveryBroadcast, payload, TimestampValidator.NowUtcMs());
                if (_udp is not null)
                {
                    await _udp.SendAsync(bytes, endpoint).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "UDP 广播发送失败");
            }

            try
            {
                await Task.Delay(_interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }
}
