using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Network.Tcp;

/// <summary>
/// 异步 TCP 考试客户端：连接教师端，含断线重连。
/// </summary>
public sealed class TcpExamClient : IDisposable
{
    private readonly PacketCodec _codec;
    private readonly ILogger<TcpExamClient>? _logger;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readCts;
    private CancellationTokenSource? _reconnectCts;
    private Task? _readTask;
    private Task? _reconnectTask;
    private string _host = string.Empty;
    private int _port;
    private bool _autoReconnect;
    private bool _disposed;

    /// <summary>
    /// 收到完整报文时触发。
    /// </summary>
    public event Func<MessageType, byte[], Task>? PacketReceived;

    /// <summary>
    /// 连接断开时触发。
    /// </summary>
    public event Action? Disconnected;

    /// <summary>
    /// 连接成功时触发。
    /// </summary>
    public event Action? Connected;

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected => _client?.Connected ?? false;

    /// <summary>
    /// 初始化客户端。
    /// </summary>
    public TcpExamClient(PacketCodec codec, ILogger<TcpExamClient>? logger = null)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _logger = logger;
    }

    /// <summary>
    /// 连接教师端。
    /// </summary>
    /// <param name="host">教师端 IP 或主机名。</param>
    /// <param name="port">教师端 TCP 端口。</param>
    /// <param name="autoReconnect">是否启用自动重连。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task ConnectAsync(string host, int port, bool autoReconnect = true, CancellationToken ct = default)
    {
        _host = host;
        _port = port;
        _autoReconnect = autoReconnect;

        await ConnectOnceAsync(ct).ConfigureAwait(false);

        if (autoReconnect)
        {
            _reconnectCts = new CancellationTokenSource();
            _reconnectTask = ReconnectLoopAsync(_reconnectCts.Token);
        }
    }

    private async Task ConnectOnceAsync(CancellationToken ct)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
        _stream = _client.GetStream();
        _readCts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token), _readCts.Token);
        Connected?.Invoke();
        _logger?.LogInformation("已连接到 {Host}:{Port}", _host, _port);
    }

    private async Task ReconnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (IsConnected)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                continue;
            }

            _logger?.LogInformation("尝试重连 {Host}:{Port}", _host, _port);
            try
            {
                await ConnectOnceAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "重连失败，3 秒后重试");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var stream = _stream!;
        var headerBuffer = new byte[PacketConstants.HeaderLength];

        while (!ct.IsCancellationRequested)
        {
            bool headerRead;
            try
            {
                headerRead = await ReadExactAsync(stream, headerBuffer, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                break;
            }

            if (!headerRead)
            {
                break;
            }

            int totalLength;
            try
            {
                totalLength = PacketReader.PeekTotalLength(headerBuffer);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "头解析失败，断开连接");
                break;
            }

            var restLength = totalLength - PacketConstants.HeaderLength;
            var restBuffer = new byte[restLength];
            bool restRead;
            try
            {
                restRead = await ReadExactAsync(stream, restBuffer, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                break;
            }

            if (!restRead)
            {
                break;
            }

            var full = new byte[totalLength];
            Buffer.BlockCopy(headerBuffer, 0, full, 0, PacketConstants.HeaderLength);
            Buffer.BlockCopy(restBuffer, 0, full, PacketConstants.HeaderLength, restLength);

            var result = _codec.Decode(full, out var messageType, out var payload);
            if (result != DecodeResult.Ok)
            {
                _logger?.LogWarning("报文校验失败：{Result}", result);
                continue;
            }

            if (PacketReceived is not null)
            {
                await PacketReceived.Invoke(messageType, payload).ConfigureAwait(false);
            }
        }

        TriggerDisconnected();
    }

    private void TriggerDisconnected()
    {
        _logger?.LogInformation("连接已断开");
        Disconnected?.Invoke();
    }

    /// <summary>
    /// 发送报文到教师端。
    /// </summary>
    public async Task SendAsync(MessageType messageType, byte[] payload)
    {
        if (_stream is null || !IsConnected)
        {
            throw new InvalidOperationException("未连接到教师端。");
        }

        var bytes = _codec.Encode(messageType, payload, TimestampValidator.NowUtcMs());
        await _stream.WriteAsync(bytes.AsMemory(0, bytes.Length)).ConfigureAwait(false);
        await _stream.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }
            offset += read;
        }
        return true;
    }

    /// <summary>
    /// 主动断开连接（停止自动重连）。
    /// </summary>
    public void Disconnect()
    {
        _reconnectCts?.Cancel();
        _readCts?.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Disconnect();
        _reconnectCts?.Dispose();
        _readCts?.Dispose();
    }
}
