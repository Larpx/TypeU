using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Data;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Network.Messages;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Larpx.PersonalTools.TypeU.Services.Student;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using ProtoBuf;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Integration;

/// <summary>
/// 端到端联调集成测试：教师端 ↔ 学生端 完整考试流程。
/// 覆盖签到登录、试题下发、状态上报、成绩回传、时间同步、重新考试。
/// </summary>
public sealed class EndToEndTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly byte[] _aesKey = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _hmacKey = RandomNumberGenerator.GetBytes(32);
    private readonly TcpExamServer _server;
    private readonly TcpExamClient _client;
    private readonly ExamRepository _examRepo;
    private readonly QuestionRepository _questionRepo;
    private readonly StudentRepository _studentRepo;
    private readonly MonitoringService _monitoring;
    private readonly TeacherExamService _examSvc;
    private readonly TypingTestService _typingTest;
    private readonly ClientTimeSyncService _clientTimeSync;

    public EndToEndTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"typeu-e2e-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(_factory).Initialize();

        var serverCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: true);
        var clientCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: false);

        _server = new TcpExamServer(serverCodec);
        _client = new TcpExamClient(clientCodec);

        _examRepo = new ExamRepository(_factory);
        _questionRepo = new QuestionRepository(_factory);
        _studentRepo = new StudentRepository(_factory);
        _monitoring = new MonitoringService();
        _examSvc = new TeacherExamService(_server, _examRepo, _questionRepo, _monitoring);
        _typingTest = new TypingTestService();
        _clientTimeSync = new ClientTimeSyncService();
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

    private async Task ConnectAsync(int port)
    {
        var clientConnected = new TaskCompletionSource<bool>();
        _client.Connected += () => clientConnected.TrySetResult(true);
        _server.Start(port);
        await _client.ConnectAsync("127.0.0.1", port, autoReconnect: false);
        await clientConnected.Task;
        await Task.Delay(100);
    }

    /// <summary>
    /// 完整考试流程：开始 → 学生收到试题 → 学生上报状态 → 学生回传成绩 → 教师端停止。
    /// </summary>
    [Fact]
    public async Task FullExamFlow_QuestionPush_StatusReport_ResultSubmit_AllDelivered()
    {
        var port = GetFreePort();
        await ConnectAsync(port);

        // 教师端准备试题。
        var qSvc = new QuestionService(_questionRepo);
        var question = qSvc.Add(QuestionType.Chinese, "春眠不觉晓，处处闻啼鸟。");

        // 学生端订阅接收 QuestionPush。
        QuestionDto? receivedQuestion = null;
        var questionTcs = new TaskCompletionSource<bool>();
        _client.PacketReceived += (type, payload) =>
        {
            if (type == MessageType.QuestionPush)
            {
                using var ms = new MemoryStream(payload);
                receivedQuestion = Serializer.Deserialize<QuestionDto>(ms);
                questionTcs.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        // 教师端开始考试。
        await _examSvc.StartAsync(ExamMode.TimedSprint, question.QuestionId, 600);

        // 验证学生端收到试题。
        await Task.WhenAny(questionTcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(questionTcs.Task.IsCompleted, "学生端未收到 QuestionPush");
        Assert.NotNull(receivedQuestion);
        Assert.Equal(question.Content, receivedQuestion!.Content);

        // 学生端设置试题并模拟输入。
        _typingTest.SetQuestion(receivedQuestion);
        _typingTest.AppendChar('春');
        _typingTest.AppendChar('眠');

        // 教师端订阅接收 StatusReport。
        StatusReportDto? receivedStatus = null;
        var statusTcs = new TaskCompletionSource<bool>();
        _server.PacketReceived += (_, type, payload) =>
        {
            if (type == MessageType.StatusReport)
            {
                using var ms = new MemoryStream(payload);
                receivedStatus = Serializer.Deserialize<StatusReportDto>(ms);
                statusTcs.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        // 学生端发送状态上报。
        var statusDto = new StatusReportDto
        {
            StudentId = "S001",
            SessionId = receivedQuestion.SessionId,
            Speed = _typingTest.GetCurrentSpeed(),
            Accuracy = _typingTest.GetCurrentAccuracy(),
            Progress = _typingTest.InputCount,
            TotalChars = _typingTest.TotalChars,
            AnomalyCount = 0,
            Timestamp = DateTime.UtcNow
        };
        using (var ms = new MemoryStream())
        {
            Serializer.Serialize(ms, statusDto);
            await _client.SendAsync(MessageType.StatusReport, ms.ToArray());
        }

        await Task.WhenAny(statusTcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(statusTcs.Task.IsCompleted, "教师端未收到 StatusReport");
        Assert.NotNull(receivedStatus);
        Assert.Equal("S001", receivedStatus!.StudentId);
        Assert.Equal(_typingTest.InputCount, receivedStatus.Progress);

        // 教师端订阅接收 ResultSubmit。
        ExamResultDto? receivedResult = null;
        var resultTcs = new TaskCompletionSource<bool>();
        _server.PacketReceived += (_, type, payload) =>
        {
            if (type == MessageType.ResultSubmit)
            {
                using var ms = new MemoryStream(payload);
                receivedResult = Serializer.Deserialize<ExamResultDto>(ms);
                resultTcs.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        // 学生端发送成绩回传。
        var resultDto = new ExamResultDto
        {
            RecordId = Guid.NewGuid(),
            SessionId = receivedQuestion.SessionId,
            StudentId = "S001",
            Speed = _typingTest.GetCurrentSpeed(),
            Accuracy = _typingTest.GetCurrentAccuracy(),
            Anomalies = string.Empty,
            SubmittedAt = DateTime.UtcNow
        };
        using (var ms = new MemoryStream())
        {
            Serializer.Serialize(ms, resultDto);
            await _client.SendAsync(MessageType.ResultSubmit, ms.ToArray());
        }

        await Task.WhenAny(resultTcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(resultTcs.Task.IsCompleted, "教师端未收到 ResultSubmit");
        Assert.NotNull(receivedResult);
        Assert.Equal("S001", receivedResult!.StudentId);

        // 教师端停止考试。
        await _examSvc.StopAsync();
        Assert.Null(_examSvc.CurrentSession);
    }

    /// <summary>
    /// 时间同步广播：教师端 TimeSyncService 广播 → 学生端 ClientTimeSyncService 接收并更新状态。
    /// </summary>
    [Fact]
    public async Task TimeSyncBroadcast_StudentReceivesAndUpdatesState()
    {
        var port = GetFreePort();
        await ConnectAsync(port);

        // 学生端订阅接收 TimeSync。
        var syncTcs = new TaskCompletionSource<bool>();
        _client.PacketReceived += (type, payload) =>
        {
            if (type == MessageType.TimeSync)
            {
                using var ms = new MemoryStream(payload);
                var msg = Serializer.Deserialize<TimeSyncMessage>(ms);
                _clientTimeSync.OnTimeSyncReceived(msg);
                syncTcs.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        // 教师端 TimeSyncService 通知开考并广播。
        var timeSyncSvc = new TimeSyncService(_server, interval: TimeSpan.FromMilliseconds(100));
        var sessionId = Guid.NewGuid();
        timeSyncSvc.NotifyExamStarted(sessionId, 600);
        timeSyncSvc.Start();

        await Task.WhenAny(syncTcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        timeSyncSvc.Stop();

        Assert.True(syncTcs.Task.IsCompleted, "学生端未收到 TimeSync 广播");
        Assert.Equal(ClientExamState.Running, _clientTimeSync.ExamState);
        Assert.Equal(sessionId, _clientTimeSync.SessionId);
        Assert.True(_clientTimeSync.HasSynced);
        Assert.True(_clientTimeSync.GetRemainingSeconds() > 0);
    }

    /// <summary>
    /// 重新考试流程：Start → Restart → 学生端应收到第二次 QuestionPush。
    /// </summary>
    [Fact]
    public async Task RestartExam_StudentReceivesSecondQuestionPush()
    {
        var port = GetFreePort();
        await ConnectAsync(port);

        var qSvc = new QuestionService(_questionRepo);
        var question = qSvc.Add(QuestionType.English, "hello world");

        var pushCount = 0;
        var secondPushTcs = new TaskCompletionSource<bool>();
        _client.PacketReceived += (type, _) =>
        {
            if (type == MessageType.QuestionPush)
            {
                pushCount++;
                if (pushCount == 2)
                {
                    secondPushTcs.TrySetResult(true);
                }
            }
            return Task.CompletedTask;
        };

        await _examSvc.StartAsync(ExamMode.FixedLength, question.QuestionId, 300);
        await _examSvc.RestartAsync();

        await Task.WhenAny(secondPushTcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(secondPushTcs.Task.IsCompleted, "学生端未收到第二次 QuestionPush");
        Assert.Equal(2, pushCount);
    }

    /// <summary>
    /// 学生端签到登录报文到达教师端（LoginAck 由教师端业务层后续实现，此处仅验证报文可达）。
    /// </summary>
    [Fact]
    public async Task StudentLoginRequest_ReachTeacherServer()
    {
        var port = GetFreePort();
        await ConnectAsync(port);

        LoginDto? receivedLogin = null;
        var loginTcs = new TaskCompletionSource<bool>();
        _server.PacketReceived += (_, type, payload) =>
        {
            if (type == MessageType.Login)
            {
                using var ms = new MemoryStream(payload);
                receivedLogin = Serializer.Deserialize<LoginDto>(ms);
                loginTcs.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        var dto = new LoginDto
        {
            StudentId = "S002",
            Name = "李四",
            DeviceFingerprint = "fp-test-002",
            ComputerName = "PC-002"
        };
        using (var ms = new MemoryStream())
        {
            Serializer.Serialize(ms, dto);
            await _client.SendAsync(MessageType.Login, ms.ToArray());
        }

        await Task.WhenAny(loginTcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.True(loginTcs.Task.IsCompleted, "教师端未收到 Login 报文");
        Assert.NotNull(receivedLogin);
        Assert.Equal("S002", receivedLogin!.StudentId);
        Assert.Equal("李四", receivedLogin.Name);
    }
}
