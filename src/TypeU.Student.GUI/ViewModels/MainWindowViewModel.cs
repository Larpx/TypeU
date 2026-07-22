using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Student.GUI.ViewModels.Pages;

namespace Larpx.PersonalTools.TypeU.Student.GUI.ViewModels;

/// <summary>
/// 学生端主窗口 ViewModel：登录页 ↔ 考试页 互斥切换。
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly LoginPageViewModel _login;
    private readonly ExamPageViewModel _exam;
    private bool _disposed;

    /// <summary>
    /// 初始化主窗口 ViewModel。
    /// </summary>
    /// <param name="login">登录页 ViewModel。</param>
    /// <param name="exam">考试页 ViewModel。</param>
    public MainWindowViewModel(LoginPageViewModel login, ExamPageViewModel exam)
    {
        _login = login ?? throw new ArgumentNullException(nameof(login));
        _exam = exam ?? throw new ArgumentNullException(nameof(exam));

        _login.LoginSucceeded += OnLoginSucceeded;
        _exam.ExamExited += OnExamExited;
        _currentPage = _login;
    }

    /// <summary>
    /// 当前显示的页面 ViewModel。
    /// </summary>
    [ObservableProperty]
    private ViewModelBase _currentPage;

    /// <summary>
    /// 是否处于沉浸式考试模式（用于 MainWindow 切换窗口样式）。
    /// </summary>
    [ObservableProperty]
    private bool _isExamImmersive;

    private void OnLoginSucceeded(string studentId)
    {
        _exam.SetStudentContext(studentId, Guid.Empty);
        CurrentPage = _exam;
        IsExamImmersive = true;
    }

    private void OnExamExited()
    {
        CurrentPage = _login;
        IsExamImmersive = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _login.LoginSucceeded -= OnLoginSucceeded;
        _exam.ExamExited -= OnExamExited;
        if (_exam is IDisposable d) d.Dispose();
        if (_login is IDisposable l) l.Dispose();
    }
}
