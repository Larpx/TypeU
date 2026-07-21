using System.ComponentModel;
using Avalonia.Controls;
using Larpx.PersonalTools.TypeU.Student.GUI.ViewModels;

namespace Larpx.PersonalTools.TypeU.Student.GUI.Views;

/// <summary>
/// 学生端主窗口：登录态下为普通窗口；考试态下进入沉浸式模式（全屏置顶、禁止移动/最小化）。
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
        if (e.PropertyName == nameof(MainWindowViewModel.IsExamImmersive)
            && DataContext is MainWindowViewModel vm)
        {
            ApplyImmersive(vm.IsExamImmersive);
        }
    }

    /// <summary>
    /// 进入/退出沉浸式：全屏 + 置顶 + 禁止最小化/还原。
    /// </summary>
    /// <param name="immersive">是否沉浸式。</param>
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
    /// 阻止考试期间通过标题栏关闭按钮退出（强制走 ViewModel 退出流程）。
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.IsExamImmersive)
        {
            // 沉浸式下禁止直接关闭，需通过 ExitExamCommand 流程退出。
            e.Cancel = true;
        }
        base.OnClosing(e);
    }
}
