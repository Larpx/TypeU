using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Network.Discovery;
using Larpx.PersonalTools.TypeU.Network.Messages;
using Larpx.PersonalTools.TypeU.Network.Security;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Network;

/// <summary>
/// UDP 发现广播器↔监听器集成测试。
/// </summary>
public class UdpDiscoveryIntegrationTests
{
    private static int GetFreeUdpPort()
    {
        using var udp = new System.Net.Sockets.UdpClient(0);
        return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }

    /// <summary>
    /// 广播器启动后，监听器应在间隔周期内收到一次广播。
    /// </summary>
    [Fact]
    public async Task BroadcasterAndListener_Loopback_ReceivesBroadcast()
    {
        var port = GetFreeUdpPort();
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var hmacKey = RandomNumberGenerator.GetBytes(32);

        using var serverCodec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);
        using var clientCodec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);

        using var broadcaster = new UdpDiscoveryBroadcaster(
            serverCodec,
            port,
            interval: TimeSpan.FromMilliseconds(200),
            targetAddress: IPAddress.Loopback);
        using var listener = new UdpDiscoveryListener(clientCodec, port);

        var tcs = new TaskCompletionSource<DiscoveryBroadcastMessage?>();
        listener.BroadcastReceived += (msg, _) => tcs.TrySetResult(msg);

        listener.Start();
        broadcaster.Start(teacherPort: 9999, teacherName: "Teacher-Test");

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(tcs.Task.IsCompleted, "监听器未在 3 秒内收到广播");

        var received = await tcs.Task;
        Assert.NotNull(received);
        Assert.Equal(9999, received!.TeacherPort);
        Assert.Equal("Teacher-Test", received.TeacherName);
    }

    /// <summary>
    /// 不同密钥的监听器不应能解码广播包。
    /// </summary>
    [Fact]
    public async Task DifferentKey_Listener_DoesNotReceiveBroadcast()
    {
        var port = GetFreeUdpPort();
        var aesKey1 = RandomNumberGenerator.GetBytes(32);
        var hmacKey1 = RandomNumberGenerator.GetBytes(32);
        var aesKey2 = RandomNumberGenerator.GetBytes(32);
        var hmacKey2 = RandomNumberGenerator.GetBytes(32);

        using var serverCodec = new PacketCodec(aesKey1, hmacKey1, verifyNonce: false);
        using var clientCodec = new PacketCodec(aesKey2, hmacKey2, verifyNonce: false);

        using var broadcaster = new UdpDiscoveryBroadcaster(
            serverCodec,
            port,
            interval: TimeSpan.FromMilliseconds(200),
            targetAddress: IPAddress.Loopback);
        using var listener = new UdpDiscoveryListener(clientCodec, port);

        var received = false;
        listener.BroadcastReceived += (_, _) => received = true;

        listener.Start();
        broadcaster.Start(teacherPort: 8888, teacherName: "Teacher-X");

        await Task.Delay(TimeSpan.FromMilliseconds(700));
        Assert.False(received, "密钥不匹配时不应收到有效广播");
    }
}
