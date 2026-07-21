using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Network;

/// <summary>
/// TCP 服务端↔客户端连通集成测试。
/// </summary>
public class TcpExamIntegrationTests : IDisposable
{
    private readonly byte[] _aesKey = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _hmacKey = RandomNumberGenerator.GetBytes(32);
    private TcpExamServer? _server;

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// 客户端连接到服务端并完成一次 echo 往返。
    /// </summary>
    [Fact]
    public async Task ClientServer_RoundTrip_PayloadEchoesBack()
    {
        var port = GetFreePort();
        using var serverCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: true);
        using var clientCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: false);

        _server = new TcpExamServer(serverCodec);
        _server.Start(port);

        string? receivedClientId = null;
        MessageType receivedType = MessageType.Unknown;
        byte[]? receivedPayload = null;
        var serverTcs = new TaskCompletionSource<bool>();
        _server.PacketReceived += (clientId, type, payload) =>
        {
            receivedClientId = clientId;
            receivedType = type;
            receivedPayload = payload;
            serverTcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        using var client = new TcpExamClient(clientCodec);
        var clientTcs = new TaskCompletionSource<bool>();
        client.Connected += () => clientTcs.TrySetResult(true);

        await client.ConnectAsync("127.0.0.1", port, autoReconnect: false);
        await clientTcs.Task;

        var payload = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE };
        await client.SendAsync(MessageType.Login, payload);

        await Task.WhenAny(serverTcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(serverTcs.Task.IsCompleted, "服务端未在 3 秒内收到报文");

        Assert.NotNull(receivedClientId);
        Assert.Equal(MessageType.Login, receivedType);
        Assert.Equal(payload, receivedPayload);
    }

    /// <summary>
    /// 服务端向客户端发送报文应能被客户端收到。
    /// </summary>
    [Fact]
    public async Task ServerToClient_Broadcast_IsReceivedByClient()
    {
        var port = GetFreePort();
        using var serverCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: true);
        using var clientCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: false);

        _server = new TcpExamServer(serverCodec);

        // 先订阅事件，再启动服务端，避免错过初始连接事件。
        string? clientId = null;
        var connectedTcs = new TaskCompletionSource<string?>();
        _server.ClientConnected += (id, _) =>
        {
            clientId = id;
            connectedTcs.TrySetResult(id);
        };

        _server.Start(port);

        using var client = new TcpExamClient(clientCodec);
        var clientTcs = new TaskCompletionSource<bool>();
        client.Connected += () => clientTcs.TrySetResult(true);

        // 客户端订阅接收事件（在发送前订阅）。
        MessageType clientMsgType = MessageType.Unknown;
        byte[]? clientPayload = null;
        var receivedTcs = new TaskCompletionSource<bool>();
        client.PacketReceived += (type, payload) =>
        {
            clientMsgType = type;
            clientPayload = payload;
            receivedTcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        await client.ConnectAsync("127.0.0.1", port, autoReconnect: false);
        await clientTcs.Task;
        await Task.WhenAny(connectedTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.NotNull(clientId);

        var payload = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        await _server.SendAsync(clientId!, MessageType.TimeSync, payload);

        await Task.WhenAny(receivedTcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(receivedTcs.Task.IsCompleted, "客户端未在 3 秒内收到服务端报文");

        Assert.Equal(MessageType.TimeSync, clientMsgType);
        Assert.Equal(payload, clientPayload);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _server?.Dispose();
    }
}
