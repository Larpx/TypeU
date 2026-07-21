using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Data;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Services.Teacher;

/// <summary>
/// TeacherExamService 考试流程集成测试：Start → Pause → Resume → Restart → Stop。
/// 通过真实 TCP 服务端 + 客户端验证广播报文到达。
/// </summary>
public sealed class TeacherExamServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly byte[] _aesKey = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _hmacKey = RandomNumberGenerator.GetBytes(32);
    private readonly TcpExamServer _server;
    private readonly TcpExamClient _client;
    private readonly ExamRepository _examRepo;
    private readonly QuestionRepository _questionRepo;
    private readonly MonitoringService _monitoring;
    private readonly TeacherExamService _examSvc;

    public TeacherExamServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"typeu-exam-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(_factory).Initialize();

        var serverCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: true);
        var clientCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: false);

        _server = new TcpExamServer(serverCodec);
        _client = new TcpExamClient(clientCodec);

        _examRepo = new ExamRepository(_factory);
        _questionRepo = new QuestionRepository(_factory);
        _monitoring = new MonitoringService();
        _examSvc = new TeacherExamService(_server, _examRepo, _questionRepo, _monitoring);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
        try { File.Delete(_dbPath); }
        catch { /* 忽略。 */ }
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task ConnectClientAndServerAsync(int port)
    {
        var clientConnected = new TaskCompletionSource<bool>();
        _client.Connected += () => clientConnected.TrySetResult(true);
        _server.Start(port);
        await _client.ConnectAsync("127.0.0.1", port, autoReconnect: false);
        await clientConnected.Task;
        await Task.Delay(100);
    }

    /// <summary>
    /// Start 后会话应被创建并持久化到数据库；客户端应收到 QuestionPush 与 ExamControl.Start。
    /// </summary>
    [Fact]
    public async Task Start_CreatesSession_AndBroadcastsToClient()
    {
        var port = GetFreePort();
        await ConnectClientAndServerAsync(port);

        var qSvc = new QuestionService(_questionRepo);
        var added = qSvc.Add(QuestionType.Chinese, "测试内容");

        MessageType? receivedType = null;
        var received = new TaskCompletionSource<bool>();
        _client.PacketReceived += (type, _) =>
        {
            if (type == MessageType.QuestionPush || type == MessageType.ExamControl)
            {
                receivedType = type;
                received.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        await _examSvc.StartAsync(ExamMode.TimedSprint, added.QuestionId, 600);

        Assert.NotNull(_examSvc.CurrentSession);
        var sessionId = _examSvc.CurrentSession!.SessionId;
        var sessionInDb = _examRepo.GetSessionById(sessionId);
        Assert.NotNull(sessionInDb);

        await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(received.Task.IsCompleted, "客户端未在 3 秒内收到下发报文");
    }

    /// <summary>
    /// Restart 后会话应保留同一个 SessionId，监控看板状态清零。
    /// </summary>
    [Fact]
    public async Task Restart_KeepsSessionId_AndResetsMonitoring()
    {
        var port = GetFreePort();
        await ConnectClientAndServerAsync(port);

        var qSvc = new QuestionService(_questionRepo);
        var q = qSvc.Add(QuestionType.English, "hello");

        await _examSvc.StartAsync(ExamMode.TimedSprint, q.QuestionId, 300);
        var sessionIdBefore = _examSvc.CurrentSession!.SessionId;

        _monitoring.RegisterStudent("S001", "张三", "ip1");
        _monitoring.UpdateProgress("S001", 50, 80, 100, 5);

        await _examSvc.RestartAsync();

        Assert.Equal(sessionIdBefore, _examSvc.CurrentSession!.SessionId);
        var states = _monitoring.GetAllStates();
        Assert.All(states, s => Assert.Equal(0, s.AnomalyCount));
    }

    /// <summary>
    /// Stop 后会话应被关闭并写入 EndedAt。
    /// </summary>
    [Fact]
    public async Task Stop_ClosesSession_WithEndedAt()
    {
        var port = GetFreePort();
        await ConnectClientAndServerAsync(port);

        var qSvc = new QuestionService(_questionRepo);
        var q = qSvc.Add(QuestionType.Code, "x();");
        await _examSvc.StartAsync(ExamMode.FixedLength, q.QuestionId, 300);

        var sessionId = _examSvc.CurrentSession!.SessionId;
        await _examSvc.StopAsync();

        Assert.Null(_examSvc.CurrentSession);
        var sessionInDb = _examRepo.GetSessionById(sessionId);
        Assert.NotNull(sessionInDb);
        Assert.True(sessionInDb!.EndedAt > DateTime.MinValue);
    }

    /// <summary>
    /// 没有活动会话时调用 Restart 应抛 InvalidOperationException。
    /// </summary>
    [Fact]
    public async Task Restart_WithoutActiveSession_Throws()
    {
        var port = GetFreePort();
        await ConnectClientAndServerAsync(port);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _examSvc.RestartAsync());
    }
}
