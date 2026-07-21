using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Network.Discovery;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Larpx.PersonalTools.TypeU.Services.Student;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Services.Student;

/// <summary>
/// StatusReportService 集成测试：定时上报、断线缓存补传。
/// </summary>
public sealed class StatusReportServiceTests
{
    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// 客户端已连接时，状态应实时上报到服务端。
    /// </summary>
    [Fact]
    public async Task Connected_StateReport_ReachesServer()
    {
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var port = GetFreePort();

        var serverCodec = new PacketCodec(aesKey, hmacKey, verifyNonce: true);
        var clientCodec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);

        using var server = new TcpExamServer(serverCodec);
        using var client = new TcpExamClient(clientCodec);

        var clientConnected = new TaskCompletionSource<bool>();
        server.ClientConnected += (id, ep) => clientConnected.TrySetResult(true);
        server.Start(port);
        await client.ConnectAsync("127.0.0.1", port, autoReconnect: false);
        await clientConnected.Task;

        MessageType? receivedType = null;
        var received = new TaskCompletionSource<bool>();
        server.PacketReceived += (_, type, _) =>
        {
            if (type == MessageType.StatusReport)
            {
                receivedType = type;
                received.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        var snapshot = new StatusReportDto
        {
            Speed = 60.0,
            Accuracy = 95.0,
            Progress = 100,
            TotalChars = 500
        };

        using var svc = new StatusReportService(client, intervalMs: 200);
        svc.Start("S001", Guid.NewGuid(), () => snapshot);

        await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(received.Task.IsCompleted, "服务端未在 3 秒内收到状态上报");
        Assert.Equal(MessageType.StatusReport, receivedType);
    }

    /// <summary>
    /// 客户端断开时，状态应入补传队列；重连后应补传到服务端。
    /// </summary>
    [Fact]
    public async Task Disconnected_StateReport_QueuedAndFlushedOnReconnect()
    {
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var port = GetFreePort();

        var serverCodec = new PacketCodec(aesKey, hmacKey, verifyNonce: true);
        var clientCodec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);

        using var server = new TcpExamServer(serverCodec);
        using var client = new TcpExamClient(clientCodec);

        server.Start(port);
        // 故意不连接 → 上报应入队。

        var snapshot = new StatusReportDto { Speed = 50, Accuracy = 90, Progress = 50, TotalChars = 200 };
        using var svc = new StatusReportService(client, intervalMs: 100);
        svc.Start("S002", Guid.NewGuid(), () => snapshot);

        // 等待几次上报周期入队。
        await Task.Delay(350);
        Assert.True(svc.PendingCount > 0, $"PendingCount={svc.PendingCount} 应大于 0");

        // 现在连接并补传。
        var clientConnected = new TaskCompletionSource<bool>();
        server.ClientConnected += (id, ep) => clientConnected.TrySetResult(true);
        await client.ConnectAsync("127.0.0.1", port, autoReconnect: false);
        await clientConnected.Task;

        var received = new TaskCompletionSource<bool>();
        server.PacketReceived += (_, type, _) =>
        {
            if (type == MessageType.StatusReport)
            {
                received.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        await svc.FlushPendingAsync();
        await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(received.Task.IsCompleted, "补传报文未到达服务端");
        Assert.Equal(0, svc.PendingCount);
    }
}

/// <summary>
/// ResultSubmitService 集成测试：成绩回传 + 失败重试 + 本地缓存。
/// </summary>
public sealed class ResultSubmitServiceTests
{
    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// 已连接时成绩应一次性回传成功。
    /// </summary>
    [Fact]
    public async Task SubmitAsync_Connected_Succeeds()
    {
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var port = GetFreePort();

        var serverCodec = new PacketCodec(aesKey, hmacKey, verifyNonce: true);
        var clientCodec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);

        using var server = new TcpExamServer(serverCodec);
        using var client = new TcpExamClient(clientCodec);

        var connected = new TaskCompletionSource<bool>();
        server.ClientConnected += (id, ep) => connected.TrySetResult(true);
        server.Start(port);
        await client.ConnectAsync("127.0.0.1", port, autoReconnect: false);
        await connected.Task;

        var received = new TaskCompletionSource<bool>();
        server.PacketReceived += (_, type, _) =>
        {
            if (type == MessageType.ResultSubmit)
            {
                received.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        var svc = new ResultSubmitService(client, maxRetries: 1, retryIntervalMs: 50);
        var result = await svc.SubmitAsync(new ExamResultDto
        {
            RecordId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            StudentId = "S001",
            Speed = 65.5,
            Accuracy = 95.0,
            Anomalies = "[]",
            SubmittedAt = DateTime.UtcNow
        });

        Assert.True(result.Success, result.ErrorMessage);
        await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(received.Task.IsCompleted);
    }

    /// <summary>
    /// 未连接时成绩应重试失败并缓存到本地文件。
    /// </summary>
    [Fact]
    public async Task SubmitAsync_Disconnected_CachesLocally()
    {
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var hmacKey = RandomNumberGenerator.GetBytes(32);
        var clientCodec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);
        using var client = new TcpExamClient(clientCodec);
        // 不连接。

        var svc = new ResultSubmitService(client, maxRetries: 2, retryIntervalMs: 50);
        var recordId = Guid.NewGuid();
        var result = await svc.SubmitAsync(new ExamResultDto
        {
            RecordId = recordId,
            SessionId = Guid.NewGuid(),
            StudentId = "S002",
            Speed = 50.0,
            Accuracy = 80.0,
            Anomalies = "[]",
            SubmittedAt = DateTime.UtcNow
        });

        Assert.False(result.Success);
        Assert.Contains("缓存", result.ErrorMessage ?? string.Empty);

        // 验证本地文件存在（不强制路径，因环境差异）。
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TypeU",
            "pending-results");
        var path = Path.Combine(dir, $"{recordId:D}.bin");
        try
        {
            Assert.True(File.Exists(path), $"缓存文件应存在：{path}");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* 忽略。 */ }
        }
    }
}
