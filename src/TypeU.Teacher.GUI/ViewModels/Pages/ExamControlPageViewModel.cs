using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

/// <summary>
/// 考试控制：开考参数含重考次数与交卷后练习。
/// </summary>
public sealed partial class ExamControlPageViewModel : ViewModelBase, IDisposable
{
    private readonly TeacherExamService? _examService;
    private readonly QuestionService? _questionService;
    private readonly ILogger<ExamControlPageViewModel>? _logger;

    /// <summary>设计时构造。</summary>
    public ExamControlPageViewModel() : this(null, null, null)
    {
    }

    /// <summary>运行时构造。</summary>
    public ExamControlPageViewModel(
        TeacherExamService? examService,
        QuestionService? questionService,
        ILogger<ExamControlPageViewModel>? logger = null)
    {
        _examService = examService;
        _questionService = questionService;
        _logger = logger;
        if (_questionService is not null)
        {
            RefreshQuestions();
        }

        if (_examService?.CurrentSession is { } s)
        {
            HasActiveSession = s.Status == ExamSessionStatus.Running;
            SessionIdText = s.SessionId.ToString("D");
            StatusText = s.Status == ExamSessionStatus.Running ? "进行中（已恢复）" : "已结束";
            MaxAttempts = s.MaxAttempts;
            AllowPracticeAfterSubmit = s.AllowPracticeAfterSubmit;
        }
    }

    /// <summary>试题列表。</summary>
    public ObservableCollection<Question> Questions { get; } = new();

    /// <summary>选中试题。</summary>
    [ObservableProperty]
    private Question? _selectedQuestion;

    /// <summary>考试模式。</summary>
    [ObservableProperty]
    private ExamMode _selectedMode = ExamMode.TimedSprint;

    /// <summary>时长（分钟）。</summary>
    [ObservableProperty]
    private int _durationMinutes = 5;

    /// <summary>重考次数 1–5。</summary>
    [ObservableProperty]
    private int _maxAttempts = 1;

    /// <summary>交齐后是否自由练习。</summary>
    [ObservableProperty]
    private bool _allowPracticeAfterSubmit;

    /// <summary>状态。</summary>
    [ObservableProperty]
    private string _statusText = "未开始";

    /// <summary>是否有进行中会话。</summary>
    [ObservableProperty]
    private bool _hasActiveSession;

    /// <summary>会话 ID。</summary>
    [ObservableProperty]
    private string _sessionIdText = string.Empty;

    /// <summary>刷新试题。</summary>
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

        if (SelectedMode == ExamMode.ErrorCorrection &&
            string.IsNullOrWhiteSpace(SelectedQuestion.ExpectedContent))
        {
            StatusText = "纠错模式需选择含参考答案的试题";
            return;
        }

        if (MaxAttempts < 1 || MaxAttempts > 5)
        {
            StatusText = "重考次数须为 1–5";
            return;
        }

        try
        {
            var durationSec = SelectedMode == ExamMode.FixedLength ? 0 : DurationMinutes * 60;
            await _examService.StartAsync(
                SelectedMode,
                SelectedQuestion.QuestionId,
                durationSec,
                MaxAttempts,
                AllowPracticeAfterSubmit);
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

    /// <summary>暂停。</summary>
    [RelayCommand]
    private async Task PauseAsync()
    {
        if (_examService is null)
        {
            return;
        }

        await _examService.PauseAsync();
        StatusText = "已暂停";
    }

    /// <summary>恢复。</summary>
    [RelayCommand]
    private async Task ResumeAsync()
    {
        if (_examService is null)
        {
            return;
        }

        await _examService.ResumeAsync();
        StatusText = "已恢复";
    }

    /// <summary>结束考试（允许学生自选登出）。</summary>
    [RelayCommand]
    private async Task StopAsync()
    {
        if (_examService is null)
        {
            return;
        }

        await _examService.StopAsync();
        HasActiveSession = false;
        StatusText = "考试已结束（学生可自行登出）";
    }

    /// <summary>重新考试。</summary>
    [RelayCommand]
    private async Task RestartAsync()
    {
        if (_examService is null)
        {
            return;
        }

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
