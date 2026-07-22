using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Larpx.PersonalTools.TypeU.Teacher.GUI.Services;
using Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels;

/// <summary>
/// 教师端主窗口 ViewModel：左侧导航 + 顶部工具栏 + 中间看板 + 右侧异常浮窗。
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ThemeService _themeService;
    private readonly TeacherExamService _examService;
    private readonly DashboardPageViewModel _dashboard;
    private readonly QuestionPageViewModel _questions;
    private readonly ExamControlPageViewModel _examControl;
    private readonly GradePageViewModel _grades;
    private readonly DeviceBindingPageViewModel _deviceBinding;
    private readonly LanScanPageViewModel _lanScan;
    private bool _disposed;

    /// <summary>
    /// 初始化主窗口 ViewModel。
    /// </summary>
    public MainWindowViewModel(
        ThemeService themeService,
        TeacherExamService examService,
        DashboardPageViewModel dashboard,
        QuestionPageViewModel questions,
        ExamControlPageViewModel examControl,
        GradePageViewModel grades,
        DeviceBindingPageViewModel deviceBinding,
        LanScanPageViewModel lanScan)
    {
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _examService = examService ?? throw new ArgumentNullException(nameof(examService));
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _questions = questions ?? throw new ArgumentNullException(nameof(questions));
        _examControl = examControl ?? throw new ArgumentNullException(nameof(examControl));
        _grades = grades ?? throw new ArgumentNullException(nameof(grades));
        _deviceBinding = deviceBinding ?? throw new ArgumentNullException(nameof(deviceBinding));
        _lanScan = lanScan ?? throw new ArgumentNullException(nameof(lanScan));

        _currentPage = _dashboard;
        Pages.Add(_dashboard);
        Pages.Add(_questions);
        Pages.Add(_examControl);
        Pages.Add(_grades);
        Pages.Add(_deviceBinding);
        Pages.Add(_lanScan);

        _themeService.PropertyChanged += (_, _) => ThemeName = _themeService.CurrentTheme.ToString();
        ThemeName = _themeService.CurrentTheme.ToString();

        if (!string.IsNullOrEmpty(_examService.TeacherId))
        {
            TeacherId = _examService.TeacherId;
            TeacherName = _examService.TeacherName;
            IsTeacherIdentified = true;
        }
    }

    /// <summary>
    /// 当前显示的页面 ViewModel。
    /// </summary>
    [ObservableProperty]
    private ViewModelBase _currentPage;

    /// <summary>
    /// 当前导航页面标识（用于按钮高亮）。
    /// </summary>
    [ObservableProperty]
    private TeacherPage _activePage = TeacherPage.Dashboard;

    /// <summary>
    /// 当前主题名称（用于显示）。
    /// </summary>
    [ObservableProperty]
    private string _themeName = "Light";

    /// <summary>教师工号。</summary>
    [ObservableProperty]
    private string _teacherId = string.Empty;

    /// <summary>教师姓名。</summary>
    [ObservableProperty]
    private string _teacherName = string.Empty;

    /// <summary>
    /// 是否已完成教师身份录入。
    /// </summary>
    [ObservableProperty]
    private bool _isTeacherIdentified;

    /// <summary>
    /// 异常告警浮窗是否展开。
    /// </summary>
    [ObservableProperty]
    private bool _isAlertPanelOpen = true;

    /// <summary>
    /// 异常告警条目。
    /// </summary>
    public ObservableCollection<AlertEntry> Alerts { get; } = new();

    /// <summary>
    /// 全部页面列表（导航栏绑定用）。
    /// </summary>
    public ObservableCollection<ViewModelBase> Pages { get; } = new();

    /// <summary>
    /// 切换到指定页面。
    /// </summary>
    [RelayCommand]
    private void Navigate(TeacherPage page)
    {
        ActivePage = page;
        CurrentPage = page switch
        {
            TeacherPage.Dashboard => _dashboard,
            TeacherPage.Questions => _questions,
            TeacherPage.ExamControl => _examControl,
            TeacherPage.Grades => _grades,
            TeacherPage.DeviceBinding => _deviceBinding,
            TeacherPage.LanScan => _lanScan,
            _ => _dashboard
        };
    }

    /// <summary>
    /// 确认教师身份。
    /// </summary>
    [RelayCommand]
    private void ConfirmTeacherIdentity()
    {
        if (string.IsNullOrWhiteSpace(TeacherId) || string.IsNullOrWhiteSpace(TeacherName))
        {
            return;
        }

        _examService.TeacherId = TeacherId.Trim();
        _examService.TeacherName = TeacherName.Trim();
        IsTeacherIdentified = true;
    }

    /// <summary>
    /// 切换主题。
    /// </summary>
    [RelayCommand]
    private void ToggleTheme() => _themeService.Toggle();

    /// <summary>
    /// 切换异常浮窗展开状态。
    /// </summary>
    [RelayCommand]
    private void ToggleAlertPanel() => IsAlertPanelOpen = !IsAlertPanelOpen;

    /// <summary>
    /// 追加一条异常告警。
    /// </summary>
    public void PushAlert(string studentId, string reason)
    {
        Alerts.Insert(0, new AlertEntry
        {
            StudentId = studentId,
            Reason = reason,
            Timestamp = DateTime.Now
        });
        if (Alerts.Count > 100)
        {
            Alerts.RemoveAt(Alerts.Count - 1);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_dashboard is IDisposable d) d.Dispose();
        if (_examControl is IDisposable e) e.Dispose();
    }
}

/// <summary>
/// 异常告警条目（用于右侧浮窗显示）。
/// </summary>
public sealed class AlertEntry
{
    /// <summary>学号。</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>异常原因。</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>告警时间。</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>格式化显示文本。</summary>
    public string DisplayText => $"[{Timestamp:HH:mm:ss}] {StudentId} - {Reason}";
}
