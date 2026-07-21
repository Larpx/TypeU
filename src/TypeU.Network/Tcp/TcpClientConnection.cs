using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Network.Tcp;

/// <summary>
/// 单个 TCP 客户端连接的读写管理（供服务端使用）。
/// </summary>
internal sealed class TcpClientConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly PacketCodec _codec;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private NetworkStream? _stream;

    public TcpClientConnection(string id, TcpClient client, PacketCodec codec, ILogger? logger)
    {
        Id = id;
        _client = client;
        _codec = codec;
        _logger = logger;
        _stream = client.GetStream();
    }

    public string Id { get; }

    public async Task ReadLoopAsync(Func<string, MessageType, byte[], Task> onPacket, CancellationToken ct)
    {
        if (_stream is null)
        {
            return;
        }

        var headerBuffer = new byte[PacketConstants.HeaderLength];

        while (!ct.IsCancellationRequested)
        {
            var headerRead = await ReadExactAsync(_stream, headerBuffer, ct).ConfigureAwait(false);
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
                _logger?.LogWarning(ex, "客户端 {ClientId} 头解析失败", Id);
                break;
            }

            var restLength = totalLength - PacketConstants.HeaderLength;
            var restBuffer = new byte[restLength];
            var restRead = await ReadExactAsync(_stream, restBuffer, ct).ConfigureAwait(false);
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
                _logger?.LogWarning("客户端 {ClientId} 报文校验失败：{Result}", Id, result);
                continue;
            }

            await onPacket(Id, messageType, payload).ConfigureAwait(false);
        }
    }

    public async Task SendAsync(byte[] bytes)
    {
        if (_stream is null)
        {
            return;
        }

        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(bytes.AsMemory(0, bytes.Length)).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
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

    public void Dispose()
    {
        _stream?.Dispose();
        _client.Dispose();
        _sendLock.Dispose();
    }
}
