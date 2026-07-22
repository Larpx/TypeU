using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Larpx.PersonalTools.TypeU.Student.GUI.ViewModels;

namespace Larpx.PersonalTools.TypeU.Student.GUI.Views;

/// <summary>
/// 学生端主窗口：登录态普通窗口；考试锁定时沉浸式；结束后可登出提示。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 初始化主窗口。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            ApplyImmersive(vm.IsExamImmersive);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsExamImmersive))
        {
            ApplyImmersive(vm.IsExamImmersive);
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty
            && DataContext is MainWindowViewModel vm)
        {
            vm.NotifyWindowMinimized(WindowState == WindowState.Minimized);
        }
    }

    /// <summary>
    /// 进入/退出沉浸式：全屏 + 置顶；登出锁定期间禁止最小化。
    /// </summary>
    private void ApplyImmersive(bool immersive)
    {
        if (immersive)
        {
            WindowState = WindowState.FullScreen;
            Topmost = true;
            CanResize = false;
            WindowDecorations = WindowDecorations.None;
            ShowInTaskbar = false;
        }
        else
        {
            WindowState = WindowState.Normal;
            Topmost = false;
            CanResize = true;
            WindowDecorations = WindowDecorations.Full;
            ShowInTaskbar = true;
        }
    }

    /// <summary>
    /// 登录锁定时禁止关闭；未锁定可关闭。
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.LogoutLocked)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
