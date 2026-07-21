using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

/// <summary>
/// 考试控制页 ViewModel：模式选择 / 时长设置 / 开始 / 暂停 / 停止 / 重新考试。
/// </summary>
public sealed partial class ExamControlPageViewModel : ViewModelBase, IDisposable
{
    private readonly TeacherExamService? _examService;
    private readonly QuestionService? _questionService;
    private readonly ILogger<ExamControlPageViewModel>? _logger;
    private readonly MonitoringService? _monitoring;

    /// <summary>设计时构造。</summary>
    public ExamControlPageViewModel() : this(null, null, null, null)
    {
    }

    /// <summary>初始化考试控制页。</summary>
    public ExamControlPageViewModel(
        TeacherExamService? examService,
        QuestionService? questionService,
        MonitoringService? monitoring,
        ILogger<ExamControlPageViewModel>? logger = null)
    {
        _examService = examService;
        _questionService = questionService;
        _monitoring = monitoring;
        _logger = logger;
        if (_questionService is not null)
        {
            RefreshQuestions();
        }
    }

    /// <summary>可选试题列表。</summary>
    public ObservableCollection<Question> Questions { get; } = new();

    /// <summary>选中的试题。</summary>
    [ObservableProperty]
    private Question? _selectedQuestion;

    /// <summary>选中的考试模式。</summary>
    [ObservableProperty]
    private ExamMode _selectedMode = ExamMode.TimedSprint;

    /// <summary>考试时长（分钟）。</summary>
    [ObservableProperty]
    private int _durationMinutes = 5;

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _statusText = "未开始";

    /// <summary>是否有活动会话。</summary>
    [ObservableProperty]
    private bool _hasActiveSession;

    /// <summary>当前会话 ID。</summary>
    [ObservableProperty]
    private string _sessionIdText = string.Empty;

    /// <summary>刷新试题列表。</summary>
    [RelayCommand]
    public void RefreshQuestions()
    {
        if (_questionService is null)
        {
            return;
        }
        Questions.Clear();
        foreach (var q in _questionService.GetAll())
        {
            Questions.Add(q);
        }
    }

    /// <summary>开始考试。</summary>
    [RelayCommand]
    private async Task StartAsync()
    {
        if (_examService is null)
        {
            StatusText = "服务未初始化";
            return;
        }
        if (SelectedQuestion is null)
        {
            StatusText = "请选择试题";
            return;
        }
        try
        {
            var durationSec = SelectedMode == ExamMode.FixedLength ? 0 : DurationMinutes * 60;
            await _examService.StartAsync(SelectedMode, SelectedQuestion.QuestionId, durationSec);
            HasActiveSession = true;
            StatusText = $"已开始：{_examService.CurrentSession?.SessionId}";
            SessionIdText = _examService.CurrentSession?.SessionId.ToString("D") ?? string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = $"失败：{ex.Message}";
            _logger?.LogError(ex, "开始考试失败");
        }
    }

    /// <summary>暂停考试。</summary>
    [RelayCommand]
    private async Task PauseAsync()
    {
        if (_examService is null) return;
        await _examService.PauseAsync();
        StatusText = "已暂停";
    }

    /// <summary>恢复考试。</summary>
    [RelayCommand]
    private async Task ResumeAsync()
    {
        if (_examService is null) return;
        await _examService.ResumeAsync();
        StatusText = "已恢复";
    }

    /// <summary>停止考试（收卷）。</summary>
    [RelayCommand]
    private async Task StopAsync()
    {
        if (_examService is null) return;
        await _examService.StopAsync();
        HasActiveSession = false;
        StatusText = "已停止";
        SessionIdText = string.Empty;
    }

    /// <summary>重新考试。</summary>
    [RelayCommand]
    private async Task RestartAsync()
    {
        if (_examService is null) return;
        try
        {
            await _examService.RestartAsync();
            StatusText = "已重新开始";
            HasActiveSession = true;
            SessionIdText = _examService.CurrentSession?.SessionId.ToString("D") ?? string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = $"失败：{ex.Message}";
            _logger?.LogError(ex, "重新考试失败");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
