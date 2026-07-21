using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Network.Tcp;

/// <summary>
/// 异步 TCP 考试服务端：监听学生端连接，解码/编码报文。
/// 支持 100 并发连接。
/// </summary>
public sealed class TcpExamServer : IDisposable
{
    private readonly PacketCodec _codec;
    private readonly ILogger<TcpExamServer>? _logger;
    private readonly ConcurrentDictionary<string, TcpClientConnection> _connections = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private int _nextClientId;

    /// <summary>
    /// 收到完整报文时触发。
    /// </summary>
    public event Func<string, MessageType, byte[], Task>? PacketReceived;

    /// <summary>
    /// 客户端断开时触发。
    /// </summary>
    public event Action<string>? ClientDisconnected;

    /// <summary>
    /// 客户端连接成功时触发。
    /// </summary>
    public event Action<string, IPEndPoint>? ClientConnected;

    /// <summary>
    /// 初始化服务端。
    /// </summary>
    /// <param name="codec">报文编解码器。</param>
    /// <param name="logger">日志（可选）。</param>
    public TcpExamServer(PacketCodec codec, ILogger<TcpExamServer>? logger = null)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _logger = logger;
    }

    /// <summary>
    /// 当前在线连接数。
    /// </summary>
    public int ConnectionCount => _connections.Count;

    /// <summary>
    /// 启动监听。
    /// </summary>
    public void Start(int port)
    {
        if (_listener is not null)
        {
            throw new InvalidOperationException("服务端已启动。");
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _acceptTask = AcceptLoopAsync(_cts.Token);
        _logger?.LogInformation("TCP 服务端已启动，监听端口 {Port}", port);
    }

    /// <summary>
    /// 停止监听并断开所有客户端。
    /// </summary>
    public void Stop()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        _listener?.Stop();

        foreach (var conn in _connections.Values)
        {
            conn.Dispose();
        }
        _connections.Clear();

        try
        {
            _acceptTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 忽略取消异常。
        }

        _cts.Dispose();
        _cts = null;
        _listener = null;
        _acceptTask = null;
        _logger?.LogInformation("TCP 服务端已停止");
    }

    /// <summary>
    /// 向指定客户端发送报文。
    /// </summary>
    public async Task SendAsync(string clientId, MessageType messageType, byte[] payload)
    {
        if (!_connections.TryGetValue(clientId, out var conn))
        {
            return;
        }

        var bytes = _codec.Encode(messageType, payload, TimestampValidator.NowUtcMs());
        await conn.SendAsync(bytes).ConfigureAwait(false);
    }

    /// <summary>
    /// 向所有在线客户端广播报文。
    /// </summary>
    public async Task BroadcastAsync(MessageType messageType, byte[] payload)
    {
        var timestamp = TimestampValidator.NowUtcMs();
        foreach (var conn in _connections.Values)
        {
            var bytes = _codec.Encode(messageType, payload, timestamp);
            await conn.SendAsync(bytes).ConfigureAwait(false);
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }

            var id = $"c{Interlocked.Increment(ref _nextClientId)}";
            var endpoint = (IPEndPoint?)client.Client.RemoteEndPoint;
            var conn = new TcpClientConnection(id, client, _codec, _logger);
            _connections[id] = conn;
            ClientConnected?.Invoke(id, endpoint!);
            _logger?.LogInformation("客户端 {ClientId} 连接自 {Endpoint}", id, endpoint);

            _ = Task.Run(() => HandleClientAsync(conn, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClientConnection conn, CancellationToken ct)
    {
        try
        {
            await conn.ReadLoopAsync(OnPacketAsync, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 正常关闭。
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "客户端 {ClientId} 读取循环异常", conn.Id);
        }
        finally
        {
            _connections.TryRemove(conn.Id, out _);
            conn.Dispose();
            ClientDisconnected?.Invoke(conn.Id);
            _logger?.LogInformation("客户端 {ClientId} 已断开", conn.Id);
        }
    }

    private Task OnPacketAsync(string clientId, MessageType messageType, byte[] payload)
    {
        return PacketReceived?.Invoke(clientId, messageType, payload) ?? Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }
}
