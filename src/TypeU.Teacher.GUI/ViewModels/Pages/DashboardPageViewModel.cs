using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Services.Teacher;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

/// <summary>
/// 监控看板页 ViewModel：学生列表 + 状态色块 + 异常红色高亮。
/// </summary>
public sealed partial class DashboardPageViewModel : ViewModelBase, IDisposable
{
    private readonly MonitoringService? _monitoring;
    private readonly TeacherExamService? _examService;

    /// <summary>
    /// 初始化监控看板（设计时用无参构造，运行时由 DI 注入服务）。
    /// </summary>
    public DashboardPageViewModel() : this(null, null)
    {
    }

    /// <summary>
    /// 初始化监控看板。
    /// </summary>
    public DashboardPageViewModel(MonitoringService? monitoring, TeacherExamService? examService)
    {
        _monitoring = monitoring;
        _examService = examService;
        if (_monitoring is not null)
        {
            _monitoring.StateUpdated += OnStateUpdated;
            _monitoring.AnomalyAlert += OnAnomalyAlert;
            Refresh();
        }
    }

    /// <summary>学生监控状态列表。</summary>
    public ObservableCollection<StudentMonitorState> Students { get; } = new();

    /// <summary>空闲学生数。</summary>
    [ObservableProperty] private int _idleCount;
    /// <summary>考试中学生数。</summary>
    [ObservableProperty] private int _examiningCount;
    /// <summary>已提交学生数。</summary>
    [ObservableProperty] private int _submittedCount;
    /// <summary>离线学生数。</summary>
    [ObservableProperty] private int _offlineCount;
    /// <summary>异常学生数。</summary>
    [ObservableProperty] private int _anomalyCount;

    /// <summary>
    /// 刷新统计数据。
    /// </summary>
    [RelayCommand]
    public void Refresh()
    {
        if (_monitoring is null)
        {
            return;
        }
        var states = _monitoring.GetAllStates();
        Students.Clear();
        foreach (var s in states)
        {
            Students.Add(s);
        }
        IdleCount = states.Count(s => s.Status == StudentStatus.Online);
        ExaminingCount = states.Count(s => s.Status == StudentStatus.Examining);
        SubmittedCount = states.Count(s => s.Status == StudentStatus.Submitted);
        OfflineCount = states.Count(s => s.Status == StudentStatus.Offline);
        AnomalyCount = states.Count(s => s.Status == StudentStatus.Anomaly);
    }

    private void OnStateUpdated(StudentMonitorState state)
    {
        // UI 线程由 Avalonia 同步上下文处理。
        var existing = Students.FirstOrDefault(s => s.StudentId == state.StudentId);
        if (existing is null)
        {
            Students.Add(state);
        }
        Refresh();
    }

    private void OnAnomalyAlert(string studentId, string reason)
    {
        // 由 MainWindowViewModel 订阅 PushAlert 到右侧浮窗。
        // 此处仅触发刷新以标红卡片。
        Refresh();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_monitoring is not null)
        {
            _monitoring.StateUpdated -= OnStateUpdated;
            _monitoring.AnomalyAlert -= OnAnomalyAlert;
        }
    }
}
