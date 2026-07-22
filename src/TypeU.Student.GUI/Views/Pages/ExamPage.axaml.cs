using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Larpx.PersonalTools.TypeU.Student.GUI.ViewModels.Pages;

namespace Larpx.PersonalTools.TypeU.Student.GUI.Views.Pages;

/// <summary>
/// 考试页视图：沉浸式考试界面，承担剪贴板/右键/拖拽/快捷键拦截入口。
/// </summary>
public partial class ExamPage : UserControl
{
    /// <summary>
    /// 初始化考试页。
    /// </summary>
    public ExamPage()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private ExamPageViewModel? Vm => DataContext as ExamPageViewModel;

    /// <summary>
    /// 页面加载完成时启动仪表盘定时刷新。
    /// </summary>
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.StartDashboard();
        }
    }

    /// <summary>
    /// 输入区文本变化：转发到 ViewModel 走防作弊过滤。
    /// </summary>
    private void HandleInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (Vm is { } vm && sender is TextBox tb)
        {
            vm.ApplyInputText(tb.Text ?? string.Empty);
        }
    }

    /// <summary>
    /// 拦截右键菜单（考试锁定模式下禁止）。
    /// </summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (Vm is { } vm && vm.ShouldBlockContextMenu())
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 拦截 Ctrl+V / Shift+Insert 粘贴（考试锁定模式下禁止）。
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (vm.ShouldBlockClipboard() &&
            ((ctrl && e.Key == Key.V) || (shift && e.Key == Key.Insert)))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 拖拽经过：考试锁定时拒绝放置。
    /// </summary>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (Vm is { } vm && vm.ShouldBlockDragDrop())
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    /// <summary>
    /// 拖拽放下：考试锁定时丢弃。
    /// </summary>
    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is { } vm && vm.ShouldBlockDragDrop())
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
        }
    }
}
