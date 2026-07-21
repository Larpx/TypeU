using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Services.Student;

namespace Larpx.PersonalTools.TypeU.Student.GUI.ViewModels.Pages;

/// <summary>
/// 登录页 ViewModel：学号/姓名输入、自动发现状态提示、手动输入教师端 IP 兜底入口、登录请求。
/// </summary>
public sealed partial class LoginPageViewModel : ViewModelBase, IDisposable
{
    private readonly StudentDiscoveryService? _discovery;
    private readonly StudentAuthService? _auth;
    private readonly ClientTimeSyncService? _timeSync;
    private readonly StatusReportService? _statusReport;
    private readonly ResultSubmitService? _resultSubmit;
    private readonly TypingTestService? _typingTest;

    /// <summary>
    /// 设计时无参构造（XAML 预览器使用）。
    /// </summary>
    public LoginPageViewModel()
    {
        StatusText = "等待自动发现教师端...";
    }

    /// <summary>
    /// 运行时构造。
    /// </summary>
    /// <param name="discovery">自动发现服务。</param>
    /// <param name="auth">签到登录服务。</param>
    /// <param name="timeSync">时间同步服务。</param>
    /// <param name="statusReport">状态上报服务。</param>
    /// <param name="resultSubmit">成绩回传服务。</param>
    /// <param name="typingTest">打字测试服务。</param>
    public LoginPageViewModel(
        StudentDiscoveryService discovery,
        StudentAuthService auth,
        ClientTimeSyncService timeSync,
        StatusReportService statusReport,
        ResultSubmitService resultSubmit,
        TypingTestService typingTest)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _timeSync = timeSync ?? throw new ArgumentNullException(nameof(timeSync));
        _statusReport = statusReport ?? throw new ArgumentNullException(nameof(statusReport));
        _resultSubmit = resultSubmit ?? throw new ArgumentNullException(nameof(resultSubmit));
        _typingTest = typingTest ?? throw new ArgumentNullException(nameof(typingTest));

        StatusText = "正在自动发现教师端...";

        _discovery.TeacherDiscovered += OnTeacherDiscovered;
        _discovery.DiscoveryTimeout += OnDiscoveryTimeout;
        _discovery.Connected += OnConnected;
    }

    /// <summary>
    /// 学号。
    /// </summary>
    [ObservableProperty]
    private string _studentId = string.Empty;

    /// <summary>
    /// 姓名。
    /// </summary>
    [ObservableProperty]
    private string _studentName = string.Empty;

    /// <summary>
    /// 自动发现状态提示。
    /// </summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// 是否已连接教师端（控制登录按钮可用性）。
    /// </summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>
    /// 是否正在登录。
    /// </summary>
    [ObservableProperty]
    private bool _isLoggingIn;

    /// <summary>
    /// 是否正在自动发现（用于显示进度提示）。
    /// </summary>
    [ObservableProperty]
    private bool _isDiscovering;

    /// <summary>
    /// 是否展开手动输入教师端 IP 区域（自动发现超时后展开）。
    /// </summary>
    [ObservableProperty]
    private bool _showManualInput;

    /// <summary>
    /// 手动输入的教师端 IP。
    /// </summary>
    [ObservableProperty]
    private string _manualHost = string.Empty;

    /// <summary>
    /// 手动输入的教师端端口。
    /// </summary>
    [ObservableProperty]
    private int _manualPort = 5800;

    /// <summary>
    /// 登录错误提示（学号/姓名空、未连接、登录失败等）。
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 登录成功事件（MainWindowViewModel 监听以切换到考试页）。
    /// </summary>
    public event Action? LoginSucceeded;

    /// <summary>
    /// 启动自动发现（页面 Loaded 时调用）。
    /// </summary>
    [RelayCommand]
    private async Task StartDiscoveryAsync()
    {
        if (_discovery is null)
        {
            return;
        }
        IsDiscovering = true;
        ShowManualInput = false;
        StatusText = "正在自动发现教师端...";
        try
        {
            await _discovery.StartDiscoveryAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = "自动发现异常：" + ex.Message;
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    /// <summary>
    /// 手动连接教师端（兜底流程）。
    /// </summary>
    [RelayCommand]
    private async Task ManualConnectAsync()
    {
        if (_discovery is null)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(ManualHost))
        {
            ErrorMessage = "请输入教师端 IP。";
            return;
        }
        ErrorMessage = string.Empty;
        StatusText = "正在连接教师端：" + ManualHost + ":" + ManualPort;
        try
        {
            await _discovery.ManuallyConnectAsync(ManualHost, ManualPort).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = "连接失败：" + ex.Message;
            StatusText = "连接失败，请检查 IP 与端口";
        }
    }

    /// <summary>
    /// 提交签到登录。
    /// </summary>
    [RelayCommand]
    private async Task LoginAsync()
    {
        if (_auth is null)
        {
            return;
        }
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(StudentId))
        {
            ErrorMessage = "请输入学号。";
            return;
        }
        if (string.IsNullOrWhiteSpace(StudentName))
        {
            ErrorMessage = "请输入姓名。";
            return;
        }
        if (!IsConnected)
        {
            ErrorMessage = "尚未连接教师端，请等待自动发现或手动输入 IP。";
            return;
        }

        IsLoggingIn = true;
        try
        {
            var result = await _auth.LoginAsync(StudentId, StudentName).ConfigureAwait(true);
            if (result.Success)
            {
                StatusText = "登录成功，等待开考...";
                LoginSucceeded?.Invoke();
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "登录失败。";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "登录异常：" + ex.Message;
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    private void OnTeacherDiscovered(System.Net.IPAddress ip, int port)
    {
        StatusText = "已发现教师端：" + ip + ":" + port;
    }

    private void OnDiscoveryTimeout()
    {
        IsDiscovering = false;
        StatusText = "自动发现超时，请手动输入教师端 IP。";
        ShowManualInput = true;
    }

    private void OnConnected()
    {
        IsConnected = true;
        IsDiscovering = false;
        StatusText = "已连接教师端，请填写学号/姓名后签到。";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_discovery is not null)
        {
            _discovery.TeacherDiscovered -= OnTeacherDiscovered;
            _discovery.DiscoveryTimeout -= OnDiscoveryTimeout;
            _discovery.Connected -= OnConnected;
        }
    }
}
