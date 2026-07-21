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
/// UDP 发现监听器（学生端使用）：启动时监听广播，收到后自动触发连接事件。
/// </summary>
public sealed class UdpDiscoveryListener : IDisposable
{
    private readonly PacketCodec _codec;
    private readonly ILogger<UdpDiscoveryListener>? _logger;
    private readonly int _listenPort;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private UdpClient? _udp;

    /// <summary>
    /// 收到教师端发现广播时触发。
    /// </summary>
    public event Action<DiscoveryBroadcastMessage, IPEndPoint>? BroadcastReceived;

    /// <summary>
    /// 初始化监听器。
    /// </summary>
    /// <param name="codec">报文编解码器。</param>
    /// <param name="listenPort">监听端口（与教师端广播端口一致）。</param>
    /// <param name="logger">日志。</param>
    public UdpDiscoveryListener(PacketCodec codec, int listenPort, ILogger<UdpDiscoveryListener>? logger = null)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _listenPort = listenPort;
        _logger = logger;
    }

    /// <summary>
    /// 启动监听。
    /// </summary>
    public void Start()
    {
        if (_cts is not null)
        {
            throw new InvalidOperationException("监听器已启动。");
        }

        _cts = new CancellationTokenSource();
        _udp = new UdpClient(_listenPort);
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
        _logger?.LogInformation("UDP 监听器已启动，端口 {Port}", _listenPort);
    }

    /// <summary>
    /// 停止监听。
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
            _listenTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 忽略取消异常。
        }

        _cts.Dispose();
        _cts = null;
        _udp = null;
        _listenTask = null;
        _logger?.LogInformation("UDP 监听器已停止");
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udp is not null)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "UDP 接收异常");
                continue;
            }

            var decode = _codec.Decode(result.Buffer, out var messageType, out var payload);
            if (decode != DecodeResult.Ok || messageType != MessageType.DiscoveryBroadcast)
            {
                _logger?.LogDebug("忽略无效 UDP 包：{Result}/{Type}", decode, messageType);
                continue;
            }

            try
            {
                using var ms = new System.IO.MemoryStream(payload);
                var msg = Serializer.Deserialize<DiscoveryBroadcastMessage>(ms);
                BroadcastReceived?.Invoke(msg, result.RemoteEndPoint);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "DiscoveryBroadcast 反序列化失败");
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }
}
