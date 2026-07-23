using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Student.GUI.ViewModels.Pages;

namespace Larpx.PersonalTools.TypeU.Student.GUI.ViewModels;

/// <summary>
/// 学生端主窗口：登录/考试切换，状态栏联网模式，登录锁定退出。
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly LoginPageViewModel _login;
    private readonly ExamPageViewModel _exam;
    private bool _disposed;

    /// <summary>设计时。</summary>
    public MainWindowViewModel()
    {
        _login = new LoginPageViewModel();
        _exam = new ExamPageViewModel();
        _currentPage = _login;
        NetworkModeText = "联网模式：单机模式";
    }

    /// <summary>
    /// 初始化。
    /// </summary>
    public MainWindowViewModel(LoginPageViewModel login, ExamPageViewModel exam)
    {
        _login = login ?? throw new ArgumentNullException(nameof(login));
        _exam = exam ?? throw new ArgumentNullException(nameof(exam));

        _login.LoginSucceeded += OnLoginSucceeded;
        _login.NetworkModeChanged += OnLoginNetworkModeChanged;
        _exam.ExamExited += OnExamExited;
        _exam.SessionUnlocked += OnSessionUnlocked;
        _exam.LogoutLockChanged += OnLogoutLockChanged;
        _currentPage = _login;
        NetworkMode = _login.NetworkMode;
        NetworkModeText = FormatNetworkMode(NetworkMode);
    }

    /// <summary>当前页。</summary>
    [ObservableProperty]
    private ViewModelBase _currentPage;

    /// <summary>沉浸式。</summary>
    [ObservableProperty]
    private bool _isExamImmersive;

    /// <summary>联网模式。</summary>
    [ObservableProperty]
    private ClientNetworkMode _networkMode = ClientNetworkMode.Offline;

    /// <summary>状态栏文案。</summary>
    [ObservableProperty]
    private string _networkModeText = "联网模式：单机模式";

    /// <summary>登录后锁定退出。</summary>
    [ObservableProperty]
    private bool _logoutLocked;

    /// <summary>考试结束后提示登出。</summary>
    [ObservableProperty]
    private bool _showLogoutPrompt;

    private void OnLoginNetworkModeChanged(ClientNetworkMode mode)
    {
        NetworkMode = mode;
        NetworkModeText = FormatNetworkMode(mode);
    }

    private static string FormatNetworkMode(ClientNetworkMode mode) => mode switch
    {
        ClientNetworkMode.Discovering => "联网模式：自动发现中",
        ClientNetworkMode.Connecting => "联网模式：正在连接教师端",
        ClientNetworkMode.Online => "联网模式：已连接教师端",
        ClientNetworkMode.Offline => "联网模式：单机模式",
        _ => "联网模式：未知"
    };

    private void OnLoginSucceeded(string studentId, PracticeMode mode)
    {
        switch (mode)
        {
            case PracticeMode.Offline:
                NetworkMode = ClientNetworkMode.Offline;
                NetworkModeText = FormatNetworkMode(NetworkMode);
                LogoutLocked = false;
                _exam.StartOfflinePractice(studentId);
                break;
            case PracticeMode.OnlinePractice:
                NetworkMode = ClientNetworkMode.Online;
                NetworkModeText = FormatNetworkMode(NetworkMode);
                LogoutLocked = false;
                _exam.StartOnlinePractice(studentId);
                break;
            case PracticeMode.Exam:
                NetworkMode = ClientNetworkMode.Online;
                NetworkModeText = "联网模式：考试已登录";
                LogoutLocked = true;
                _exam.BeginOnlineExamSession(
                    studentId,
                    _login.LastMaxAttempts,
                    _login.LastAllowPractice);
                break;
        }

        CurrentPage = _exam;
        IsExamImmersive = true;
        ShowLogoutPrompt = false;
    }

    private void OnExamExited()
    {
        CurrentPage = _login;
        IsExamImmersive = false;
        LogoutLocked = false;
        ShowLogoutPrompt = false;
        NetworkMode = _login.NetworkMode;
        NetworkModeText = FormatNetworkMode(NetworkMode);
    }

    private void OnSessionUnlocked()
    {
        IsExamImmersive = false;
    }

    private void OnLogoutLockChanged(bool locked)
    {
        LogoutLocked = locked;
        if (!locked && CurrentPage == _exam)
        {
            ShowLogoutPrompt = true;
            NetworkModeText = "联网模式：考试已结束，可登出";
        }
    }

    /// <summary>返回登录（未锁定时）。</summary>
    [RelayCommand]
    private void BackToLogin()
    {
        if (LogoutLocked)
        {
            return;
        }

        OnExamExited();
    }

    /// <summary>确认登出并清除本机信息。</summary>
    [RelayCommand]
    private async System.Threading.Tasks.Task ConfirmLogoutAsync()
    {
        await _exam.RequestLogoutAsync().ConfigureAwait(true);
        OnExamExited();
    }

    /// <summary>暂不登出，继续停留。</summary>
    [RelayCommand]
    private void DismissLogoutPrompt()
    {
        ShowLogoutPrompt = false;
    }

    /// <summary>同步窗口最小化状态到考试页上报。</summary>
    public void NotifyWindowMinimized(bool minimized) => _exam.SetWindowMinimized(minimized);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _login.LoginSucceeded -= OnLoginSucceeded;
        _login.NetworkModeChanged -= OnLoginNetworkModeChanged;
        _exam.ExamExited -= OnExamExited;
        _exam.SessionUnlocked -= OnSessionUnlocked;
        _exam.LogoutLockChanged -= OnLogoutLockChanged;
        if (_exam is IDisposable d)
        {
            d.Dispose();
        }

        if (_login is IDisposable l)
        {
            l.Dispose();
        }
    }
}
