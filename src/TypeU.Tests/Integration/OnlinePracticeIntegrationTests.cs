using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Data;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using ProtoBuf;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Integration;

/// <summary>
/// 联网练习与断线重连集成测试：验证未开考时学生状态上报、断线清理、重连识别与开考状态下发。
/// </summary>
public sealed class OnlinePracticeIntegrationTests : IDisposable
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
    private readonly SessionLoginRepository _sessionLogins;
    private readonly MonitoringService _monitoring;
    private readonly DeviceBindingService _deviceBinding;
    private readonly GradeService _gradeService;
    private readonly TimeSyncService _timeSync;
    private readonly TeacherExamService _examSvc;
    private readonly TeacherPacketHandler _handler;

    public OnlinePracticeIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"typeu-prac-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(_factory).Initialize();

        var serverCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: true);
        var clientCodec = new PacketCodec(_aesKey, _hmacKey, verifyNonce: false);

        _server = new TcpExamServer(serverCodec);
        _client = new TcpExamClient(clientCodec);

        _examRepo = new ExamRepository(_factory);
        _questionRepo = new QuestionRepository(_factory);
        _studentRepo = new StudentRepository(_factory);
        _sessionLogins = new SessionLoginRepository(_factory);
        _monitoring = new MonitoringService();
        _deviceBinding = new DeviceBindingService(_studentRepo);
        _gradeService = new GradeService(_examRepo, _studentRepo);
        _timeSync = new TimeSyncService(_server);
        _examSvc = new TeacherExamService(_server, _examRepo, _questionRepo, _monitoring, _timeSync);
        _handler = new TeacherPacketHandler(_server, _deviceBinding, _monitoring, _gradeService, _examSvc, _sessionLogins);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _handler.Dispose();
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

    private async Task<string> ConnectAsync(int port)
    {
        var clientIdTcs = new TaskCompletionSource<string>();
        void OnConnected(string id, System.Net.IPEndPoint ep) => clientIdTcs.TrySetResult(id);
        _server.ClientConnected += OnConnected;
        _server.Start(port);
        await _client.ConnectAsync("127.0.0.1", port, autoReconnect: false);
        var clientId = await clientIdTcs.Task;
        _server.ClientConnected -= OnConnected;
        await Task.Delay(100);
        return clientId;
    }

    private static async Task SendProtoAsync<T>(TcpExamClient client, MessageType type, T dto)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, dto);
        await client.SendAsync(type, ms.ToArray());
    }

    private static async Task<HelloAckDto?> ReceiveHelloAckAsync(TcpExamClient client, TimeSpan timeout)
    {
        HelloAckDto? ack = null;
        var tcs = new TaskCompletionSource<bool>();
        Task OnPacket(MessageType type, byte[] payload)
        {
            if (type == MessageType.HelloAck)
            {
                using var ms = new MemoryStream(payload);
                ack = Serializer.Deserialize<HelloAckDto>(ms);
                tcs.TrySetResult(true);
            }

            return Task.CompletedTask;
        }

        client.PacketReceived += OnPacket;
        await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        client.PacketReceived -= OnPacket;
        return ack;
    }

    private static HelloDto BuildHello(string fingerprint, string computerName) => new()
    {
        DeviceFingerprint = fingerprint,
        ComputerName = computerName,
        ClientIp = "127.0.0.1"
    };

    /// <summary>
    /// 未开考时学生端发 Hello + StatusReport（联网练习），教师端 MonitoringService 应记录设备状态。
    /// </summary>
    [Fact]
    public async Task OnlinePractice_StatusReport_RecordsMonitoringState()
    {
        var port = GetFreePort();
        var clientId = await ConnectAsync(port);

        // 学生发 Hello（未开考，HelloAck.ExamRunning=false）。
        await SendProtoAsync(_client, MessageType.Hello, BuildHello("fp-prac-001", "PC-PRAC"));
        var ack = await ReceiveHelloAckAsync(_client, TimeSpan.FromSeconds(3));
        Assert.NotNull(ack);
        Assert.False(ack!.ExamRunning);

        // 学生发 StatusReport（联网练习：上报速度/最小化/模式，不计成绩）。
        await SendProtoAsync(_client, MessageType.StatusReport, new StatusReportDto
        {
            Speed = 35.5,
            Accuracy = 96.0,
            Progress = 10,
            TotalChars = 100,
            IsMinimized = false,
            ClientMode = "联网练习",
            DeviceFingerprint = "fp-prac-001",
            Timestamp = DateTime.UtcNow
        });
        await Task.Delay(300);

        var state = _monitoring.GetAllStates().FirstOrDefault(s => s.ClientId == clientId);
        Assert.NotNull(state);
        Assert.Equal("联网练习", state!.ClientMode);
        Assert.Equal(35.5, state.Speed);
        Assert.Equal("fp-prac-001", state.DeviceFingerprint);
        Assert.False(state.IsLoggedIn);
    }

    /// <summary>
    /// 学生断线后教师端标记 Offline；新连接重连后重新标记 Online。
    /// </summary>
    [Fact]
    public async Task Disconnect_MarksOffline_Reconnect_RecordsOnline()
    {
        var port = GetFreePort();
        var clientId = await ConnectAsync(port);
        await SendProtoAsync(_client, MessageType.Hello, BuildHello("fp-reconnect-001", "PC-RECONN"));
        await Task.Delay(200);

        // 断线：教师端应标记 Offline。
        _client.Disconnect();
        await Task.Delay(500);

        var offlineState = _monitoring.GetAllStates().FirstOrDefault(s => s.ClientId == clientId);
        Assert.NotNull(offlineState);
        Assert.Equal(StudentStatus.Offline, offlineState!.Status);

        // 重连（新 client，同设备指纹）。
        var client2 = new TcpExamClient(new PacketCodec(_aesKey, _hmacKey, verifyNonce: false));
        var newClientIdTcs = new TaskCompletionSource<string>();
        void OnNewConnected(string id, System.Net.IPEndPoint ep) => newClientIdTcs.TrySetResult(id);
        _server.ClientConnected += OnNewConnected;
        try
        {
            await client2.ConnectAsync("127.0.0.1", port, autoReconnect: false);
            var newClientId = await newClientIdTcs.Task;
            await Task.Delay(200);

            var newState = _monitoring.GetAllStates().FirstOrDefault(s => s.ClientId == newClientId);
            Assert.NotNull(newState);
            Assert.Equal(StudentStatus.Online, newState!.Status);
        }
        finally
        {
            _server.ClientConnected -= OnNewConnected;
            client2.Dispose();
        }
    }

    /// <summary>
    /// 学生未开考时连接 → 教师开考 → 学生断线重连 → HelloAck 应反映 ExamRunning=true。
    /// </summary>
    [Fact]
    public async Task Reconnect_AfterExamStarted_HelloAckReflectsExamRunning()
    {
        var port = GetFreePort();
        await ConnectAsync(port);

        // 首次连接（未开考）。
        await SendProtoAsync(_client, MessageType.Hello, BuildHello("fp-reconnect-002", "PC-RECONN2"));
        var ack1 = await ReceiveHelloAckAsync(_client, TimeSpan.FromSeconds(3));
        Assert.NotNull(ack1);
        Assert.False(ack1!.ExamRunning);

        // 教师开考。
        var qSvc = new QuestionService(_questionRepo);
        var question = qSvc.Add(QuestionType.Chinese, "断线重连测试内容");
        await _examSvc.StartAsync(ExamMode.TimedSprint, question.QuestionId, 600, maxAttempts: 1, allowPracticeAfterSubmit: false);

        // 学生断线。
        _client.Disconnect();
        await Task.Delay(500);

        // 重连（新 client，同设备指纹）。
        var client2 = new TcpExamClient(new PacketCodec(_aesKey, _hmacKey, verifyNonce: false));
        var reconnectTcs = new TaskCompletionSource<bool>();
        _server.ClientConnected += OnReconnect;
        try
        {
            await client2.ConnectAsync("127.0.0.1", port, autoReconnect: false);
            await reconnectTcs.Task;
            await Task.Delay(100);

            await SendProtoAsync(client2, MessageType.Hello, BuildHello("fp-reconnect-002", "PC-RECONN2"));
            var ack2 = await ReceiveHelloAckAsync(client2, TimeSpan.FromSeconds(3));
            Assert.NotNull(ack2);
            Assert.True(ack2!.ExamRunning);
            Assert.Equal(_examSvc.CurrentSession!.SessionId, ack2.SessionId);
        }
        finally
        {
            _server.ClientConnected -= OnReconnect;
            client2.Dispose();
        }

        void OnReconnect(string id, System.Net.IPEndPoint ep) => reconnectTcs.TrySetResult(true);
    }
}
