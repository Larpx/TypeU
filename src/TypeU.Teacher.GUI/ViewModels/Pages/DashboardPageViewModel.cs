using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

/// <summary>
/// 监控看板：连接/登录/模式/速度/最小化 + 允许登出。
/// </summary>
public sealed partial class DashboardPageViewModel : ViewModelBase, IDisposable
{
    private readonly MonitoringService? _monitoring;
    private readonly TeacherPacketHandler? _packetHandler;
    private readonly ILogger<DashboardPageViewModel>? _logger;

    /// <summary>设计时。</summary>
    public DashboardPageViewModel() : this(null, null, null)
    {
    }

    /// <summary>运行时。</summary>
    public DashboardPageViewModel(
        MonitoringService? monitoring,
        TeacherPacketHandler? packetHandler,
        ILogger<DashboardPageViewModel>? logger = null)
    {
        _monitoring = monitoring;
        _packetHandler = packetHandler;
        _logger = logger;
        if (_monitoring is not null)
        {
            _monitoring.StateUpdated += OnStateUpdated;
            _monitoring.AnomalyAlert += OnAnomalyAlert;
            Refresh();
        }
    }

    /// <summary>学生列表。</summary>
    public ObservableCollection<StudentMonitorState> Students { get; } = new();

    /// <summary>选中行。</summary>
    [ObservableProperty]
    private StudentMonitorState? _selectedStudent;

    /// <summary>空闲。</summary>
    [ObservableProperty] private int _idleCount;
    /// <summary>考试中。</summary>
    [ObservableProperty] private int _examiningCount;
    /// <summary>已提交。</summary>
    [ObservableProperty] private int _submittedCount;
    /// <summary>离线。</summary>
    [ObservableProperty] private int _offlineCount;
    /// <summary>异常。</summary>
    [ObservableProperty] private int _anomalyCount;

    /// <summary>刷新。</summary>
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

        IdleCount = states.Count(s => s.Status == StudentStatus.Online && !s.IsLoggedIn);
        ExaminingCount = states.Count(s => s.Status == StudentStatus.Examining || (s.IsLoggedIn && s.Status == StudentStatus.Online));
        SubmittedCount = states.Count(s => s.Status == StudentStatus.Submitted);
        OfflineCount = states.Count(s => s.Status == StudentStatus.Offline);
        AnomalyCount = states.Count(s => s.Status == StudentStatus.Anomaly);
    }

    /// <summary>允许指定学生登出。</summary>
    [RelayCommand]
    private async Task AllowLogoutAsync(StudentMonitorState? row)
    {
        if (_packetHandler is null || row is null || string.IsNullOrWhiteSpace(row.StudentId))
        {
            return;
        }

        try
        {
            await _packetHandler.AllowLogoutAsync(row.StudentId).ConfigureAwait(true);
            row.LogoutAllowed = true;
            _logger?.LogInformation("已允许 {StudentId} 登出", row.StudentId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "允许登出失败");
        }
    }

    private void OnStateUpdated(StudentMonitorState state) => Refresh();

    private void OnAnomalyAlert(string studentId, string reason) => Refresh();

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
