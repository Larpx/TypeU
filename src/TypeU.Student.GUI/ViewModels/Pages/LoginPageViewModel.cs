using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Larpx.PersonalTools.TypeU.Services.Student;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Student.GUI.ViewModels.Pages;

/// <summary>
/// 登录页：默认单机；配置 IP 后 Ping→连接→Hello；仅开考后登录。
/// </summary>
public sealed partial class LoginPageViewModel : ViewModelBase, IDisposable
{
    private readonly StudentConnectionService? _connection;
    private readonly StudentAuthService? _auth;
    private readonly TcpExamClient? _client;
    private readonly ILogger<LoginPageViewModel>? _logger;
    private StudentClientConfig _config = new();
    private bool _disposed;

    /// <summary>设计时。</summary>
    public LoginPageViewModel()
    {
        StatusText = "单机模式";
        NetworkMode = ClientNetworkMode.Offline;
        IsOfflineMode = true;
    }

    /// <summary>运行时。</summary>
    public LoginPageViewModel(
        StudentConnectionService connection,
        StudentAuthService auth,
        TcpExamClient client,
        ILogger<LoginPageViewModel>? logger = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;

        _config = StudentClientConfig.Load();
        ManualHost = _config.TeacherHost;
        ManualPort = _config.TeacherPort;
        IsOfflineMode = true;
        NetworkMode = ClientNetworkMode.Offline;
        StatusText = "单机模式。可配置教师 IP 后连接。";
        CanLogin = false;
        _client.PacketReceived += OnPacketReceived;
    }

    /// <summary>学号。</summary>
    [ObservableProperty] private string _studentId = string.Empty;
    /// <summary>姓名。</summary>
    [ObservableProperty] private string _studentName = string.Empty;
    /// <summary>状态。</summary>
    [ObservableProperty] private string _statusText = string.Empty;
    /// <summary>是否已连接。</summary>
    [ObservableProperty] private bool _isConnected;
    /// <summary>是否单机。</summary>
    [ObservableProperty] private bool _isOfflineMode = true;
    /// <summary>联网模式。</summary>
    [ObservableProperty] private ClientNetworkMode _networkMode = ClientNetworkMode.Offline;
    /// <summary>是否正在登录。</summary>
    [ObservableProperty] private bool _isLoggingIn;
    /// <summary>是否允许登录（开考后）。</summary>
    [ObservableProperty] private bool _canLogin;
    /// <summary>考试是否进行中。</summary>
    [ObservableProperty] private bool _examRunning;
    /// <summary>教师 IP。</summary>
    [ObservableProperty] private string _manualHost = string.Empty;
    /// <summary>教师端口。</summary>
    [ObservableProperty] private int _manualPort = 5700;
    /// <summary>错误。</summary>
    [ObservableProperty] private string _errorMessage = string.Empty;
    /// <summary>最大次数。</summary>
    [ObservableProperty] private int _maxAttempts = 1;
    /// <summary>交卷后练习。</summary>
    [ObservableProperty] private bool _allowPracticeAfterSubmit;

    /// <summary>主按钮文案。</summary>
    public string LoginButtonText =>
        IsOfflineMode ? "开始单机练习" : (CanLogin ? "考试登录" : "等待开考（可联网练习）");

    /// <summary>登录成功（学号, 是否单机）。</summary>
    public event Action<string, bool>? LoginSucceeded;

    /// <summary>联网模式变更。</summary>
    public event Action<ClientNetworkMode>? NetworkModeChanged;

    /// <summary>自动登录上下文（次数等）。</summary>
    public int LastMaxAttempts => MaxAttempts;

    /// <summary>交卷后练习标志。</summary>
    public bool LastAllowPractice => AllowPracticeAfterSubmit;

    partial void OnNetworkModeChanged(ClientNetworkMode value) => NetworkModeChanged?.Invoke(value);

    partial void OnIsOfflineModeChanged(bool value) => OnPropertyChanged(nameof(LoginButtonText));

    partial void OnCanLoginChanged(bool value) => OnPropertyChanged(nameof(LoginButtonText));

    /// <summary>
    /// 启动：默认单机；若配置了 IP 则尝试 Ping+连接。
    /// </summary>
    [RelayCommand]
    private async Task StartDiscoveryAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(ManualHost))
        {
            EnterOffline("未配置教师 IP，保持单机模式。");
            return;
        }

        NetworkMode = ClientNetworkMode.Connecting;
        StatusText = "正在 Ping 教师端...";
        if (!await StudentConnectionService.PingHostAsync(ManualHost).ConfigureAwait(true))
        {
            EnterOffline("Ping 失败，保持单机模式。可检查 IP 后重试。");
            return;
        }

        if (_connection is null)
        {
            return;
        }

        try
        {
            StatusText = "Ping 通，正在连接...";
            var ack = await _connection.ConnectAndHelloAsync(ManualHost, ManualPort).ConfigureAwait(true);
            if (ack is null)
            {
                EnterOffline("握手超时，保持单机模式。");
                return;
            }

            ApplyHelloAck(ack);
            SaveConfig();
        }
        catch (Exception ex)
        {
            EnterOffline("连接失败：" + ex.Message);
            _logger?.LogWarning(ex, "连接教师端失败");
        }
    }

    /// <summary>
    /// 保存并连接。
    /// </summary>
    [RelayCommand]
    private async Task ManualConnectAsync()
    {
        SaveConfig();
        await StartDiscoveryAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// 登录或单机练习。
    /// </summary>
    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(StudentId) || string.IsNullOrWhiteSpace(StudentName))
        {
            ErrorMessage = "请输入学号与姓名。";
            return;
        }

        if (IsOfflineMode)
        {
            LoginSucceeded?.Invoke(StudentId, true);
            return;
        }

        if (!CanLogin || _auth is null)
        {
            ErrorMessage = ExamRunning ? "请稍候..." : "当前未开考，仅可联网练习或等待教师开考后登录。";
            return;
        }

        IsLoggingIn = true;
        try
        {
            var result = await _auth.LoginAsync(StudentId, StudentName).ConfigureAwait(true);
            if (result.Success)
            {
                MaxAttempts = result.MaxAttempts > 0 ? result.MaxAttempts : 1;
                AllowPracticeAfterSubmit = result.AllowPracticeAfterSubmit;
                StatusText = "登录成功";
                NetworkMode = ClientNetworkMode.Online;
                LoginSucceeded?.Invoke(StudentId, false);
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "登录失败";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    /// <summary>
    /// 应用开考广播后允许登录。
    /// </summary>
    public void OnExamStarted(int maxAttempts, bool allowPractice)
    {
        ExamRunning = true;
        CanLogin = true;
        MaxAttempts = maxAttempts;
        AllowPracticeAfterSubmit = allowPractice;
        StatusText = "考试已开始，请登录。";
        OnPropertyChanged(nameof(LoginButtonText));
    }

    /// <summary>
    /// 应用自动登录。
    /// </summary>
    public void ApplyAutoLogin(string studentId, string name, int maxAttempts, bool allowPractice)
    {
        StudentId = studentId;
        StudentName = name;
        MaxAttempts = maxAttempts;
        AllowPracticeAfterSubmit = allowPractice;
        ExamRunning = true;
        CanLogin = true;
        IsOfflineMode = false;
        IsConnected = true;
        NetworkMode = ClientNetworkMode.Online;
        StatusText = "已自动登录：" + studentId;
        LoginSucceeded?.Invoke(studentId, false);
    }

    private void ApplyHelloAck(Models.Dtos.HelloAckDto ack)
    {
        IsConnected = true;
        IsOfflineMode = false;
        NetworkMode = ClientNetworkMode.Online;
        ExamRunning = ack.ExamRunning;
        CanLogin = ack.ExamRunning;
        MaxAttempts = ack.MaxAttempts > 0 ? ack.MaxAttempts : 1;
        AllowPracticeAfterSubmit = ack.AllowPracticeAfterSubmit;

        if (ack.AutoLogin && !string.IsNullOrEmpty(ack.StudentId))
        {
            ApplyAutoLogin(ack.StudentId, ack.StudentName, MaxAttempts, AllowPracticeAfterSubmit);
            return;
        }

        StatusText = ack.ExamRunning
            ? "已连接教师端，考试进行中，请登录。"
            : "已连接教师端（未开考），可练习，开考后登录。";
        OnPropertyChanged(nameof(LoginButtonText));
    }

    private void EnterOffline(string status)
    {
        IsOfflineMode = true;
        IsConnected = false;
        CanLogin = false;
        ExamRunning = false;
        NetworkMode = ClientNetworkMode.Offline;
        StatusText = status;
    }

    private void SaveConfig()
    {
        _config.TeacherHost = ManualHost?.Trim() ?? string.Empty;
        _config.TeacherPort = ManualPort;
        _config.Save();
    }

    private Task OnPacketReceived(MessageType type, byte[] payload)
    {
        if (type != MessageType.ExamLifecycle)
        {
            return Task.CompletedTask;
        }

        try
        {
            using var ms = new MemoryStream(payload);
            var life = Serializer.Deserialize<ExamLifecycleDto>(ms);
            if (life.Started)
            {
                OnExamStarted(life.MaxAttempts > 0 ? life.MaxAttempts : 1, life.AllowPracticeAfterSubmit);
            }
            else
            {
                ExamRunning = false;
                CanLogin = false;
                StatusText = string.IsNullOrEmpty(life.Message)
                    ? "考试已结束。"
                    : life.Message;
                OnPropertyChanged(nameof(LoginButtonText));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ExamLifecycle 解析失败");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_client is not null)
        {
            _client.PacketReceived -= OnPacketReceived;
        }
    }
}
