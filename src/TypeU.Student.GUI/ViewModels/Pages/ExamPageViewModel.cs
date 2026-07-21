using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Core.AntiCheat;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Larpx.PersonalTools.TypeU.Services.Student;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Student.GUI.ViewModels.Pages;

/// <summary>
/// 单字符渲染状态（供 XAML 高亮绑定）。
/// </summary>
public sealed class CharRenderState
{
    /// <summary>字符。</summary>
    public char Character { get; init; }

    /// <summary>是否已输入。</summary>
    public bool IsEntered { get; init; }

    /// <summary>是否正确（已输入且与原文一致）。</summary>
    public bool IsCorrect { get; init; }

    /// <summary>是否错误（已输入且与原文不一致）。</summary>
    public bool IsWrong { get; init; }
}

/// <summary>
/// 考试页 ViewModel：沉浸式原文/输入/高亮/仪表盘/防粘贴。
/// </summary>
public sealed partial class ExamPageViewModel : ViewModelBase, IDisposable
{
    private readonly TypingTestService _typingTest;
    private readonly ClientTimeSyncService _timeSync;
    private readonly StatusReportService _statusReport;
    private readonly ResultSubmitService _resultSubmit;
    private readonly TcpExamClient _client;
    private readonly AntiCheatMonitor _antiCheat;
    private readonly InputProtectionPolicy _protection;
    private readonly ILogger<ExamPageViewModel>? _logger;
    private Timer? _dashboardTimer;
    private string _studentId = string.Empty;
    private Guid _sessionId;
    private bool _disposed;

    /// <summary>
    /// 设计时无参构造（XAML 预览器使用）。
    /// </summary>
    public ExamPageViewModel()
    {
        _typingTest = null!;
        _timeSync = null!;
        _statusReport = null!;
        _resultSubmit = null!;
        _client = null!;
        _antiCheat = null!;
        _protection = null!;
        OriginalText = "（设计时示例）欢迎使用 TypeU 学生端。";
        foreach (var c in OriginalText)
        {
            Chars.Add(new CharRenderState { Character = c });
        }
    }

    /// <summary>
    /// 运行时构造。
    /// </summary>
    public ExamPageViewModel(
        TypingTestService typingTest,
        ClientTimeSyncService timeSync,
        StatusReportService statusReport,
        ResultSubmitService resultSubmit,
        TcpExamClient client,
        AntiCheatMonitor antiCheat,
        InputProtectionPolicy protection,
        ILogger<ExamPageViewModel>? logger = null)
    {
        _typingTest = typingTest ?? throw new ArgumentNullException(nameof(typingTest));
        _timeSync = timeSync ?? throw new ArgumentNullException(nameof(timeSync));
        _statusReport = statusReport ?? throw new ArgumentNullException(nameof(statusReport));
        _resultSubmit = resultSubmit ?? throw new ArgumentNullException(nameof(resultSubmit));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _antiCheat = antiCheat ?? throw new ArgumentNullException(nameof(antiCheat));
        _protection = protection ?? throw new ArgumentNullException(nameof(protection));
        _logger = logger;

        _client.PacketReceived += OnPacketReceived;
    }

    /// <summary>
    /// 试题原文（行号渲染基准）。
    /// </summary>
    [ObservableProperty]
    private string _originalText = string.Empty;

    /// <summary>
    /// 学生输入区文本（双向绑定）。
    /// </summary>
    [ObservableProperty]
    private string _inputText = string.Empty;

    /// <summary>
    /// 字符渲染列表（每个字符一个 CharRenderState）。
    /// </summary>
    public ObservableCollection<CharRenderState> Chars { get; } = new();

    /// <summary>
    /// 剩余秒数（用于仪表盘与倒计时）。
    /// </summary>
    [ObservableProperty]
    private int _remainingSeconds;

    /// <summary>
    /// 总考试秒数（用于仪表盘进度计算）。
    /// </summary>
    [ObservableProperty]
    private int _totalSeconds = 1;

    /// <summary>
    /// 实时速度（字/分钟）。
    /// </summary>
    [ObservableProperty]
    private double _currentSpeed;

    /// <summary>
    /// 实时正确率（0-100）。
    /// </summary>
    [ObservableProperty]
    private double _currentAccuracy;

    /// <summary>
    /// 已输入字符数。
    /// </summary>
    [ObservableProperty]
    private int _inputCount;

    /// <summary>
    /// 总字符数。
    /// </summary>
    [ObservableProperty]
    private int _totalChars;

    /// <summary>
    /// 考试状态文本（开考/进行中/暂停/已结束）。
    /// </summary>
    [ObservableProperty]
    private string _examStateText = "等待开考...";

    /// <summary>
    /// 异常告警文本（防粘贴/批量上屏提示）。
    /// </summary>
    [ObservableProperty]
    private string _alertText = string.Empty;

    /// <summary>
    /// 是否正在考试（用于退出按钮可用性）。
    /// </summary>
    [ObservableProperty]
    private bool _isExamRunning;

    /// <summary>
    /// 退出考试事件（MainWindowViewModel 监听以切回登录页）。
    /// </summary>
    public event Action? ExamExited;

    /// <summary>
    /// 仪表盘进度（0-100）：剩余时间占比。
    /// </summary>
    public double DashboardProgress => TotalSeconds > 0
        ? Math.Max(0, Math.Min(100, RemainingSeconds * 100.0 / TotalSeconds))
        : 0;

    /// <summary>
    /// 倒计时显示文本（HH:MM:SS）。
    /// </summary>
    public string RemainingDisplay
    {
        get
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, RemainingSeconds));
            return t.ToString(@"hh\:mm\:ss");
        }
    }

    /// <summary>
    /// 设置学号与会话（登录成功后调用）。
    /// </summary>
    public void SetStudentContext(string studentId, Guid sessionId)
    {
        _studentId = studentId ?? string.Empty;
        _sessionId = sessionId;
    }

    /// <summary>
    /// 启动仪表盘定时刷新（页面 Loaded 时调用）。
    /// </summary>
    [RelayCommand]
    public void StartDashboard()
    {
        _dashboardTimer?.Dispose();
        _dashboardTimer = new Timer(_ => UpdateDashboard(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 处理输入文本变化（XAML 输入区 TextChanged 触发）。
    /// 走防作弊监控、增量比对、状态上报快照获取。
    /// </summary>
    /// <param name="newText">输入区当前完整文本。</param>
    public void ApplyInputText(string newText)
    {
        if (_protection is null || !_protection.IsExamLocked)
        {
            return;
        }

        var oldLen = _typingTest.InputCount;
        var newLen = newText?.Length ?? 0;

        if (newLen > oldLen)
        {
            // 新增字符数（一次输入事件可能含多个字符，比如输入法批量上屏）。
            var added = newLen - oldLen;
            var verify = _antiCheat.Verify(added, Environment.TickCount64);
            if (verify.Anomaly)
            {
                _statusReport?.IncrementAnomaly();
                AlertText = "检测到异常输入：" + verify.Reason;
            }
            else
            {
                AlertText = string.Empty;
            }

            // 仅取有效字符计入成绩（ValidCount 限制为 MaxValidCharsPerEvent）。
            var valid = Math.Min(verify.ValidCount, added);
            for (var i = 0; i < valid; i++)
            {
                var ch = newText![oldLen + i];
                _typingTest.AppendChar(ch);
            }
            // 超出有效部分回滚输入区文本。
            if (added > valid)
            {
                InputText = newText!.Substring(0, oldLen + valid);
            }
        }
        else if (newLen < oldLen)
        {
            // 退格：直接同步 TypingTestService 状态。
            var removed = oldLen - newLen;
            for (var i = 0; i < removed; i++)
            {
                _typingTest.Backspace();
            }
            AlertText = string.Empty;
        }

        RefreshChars();
        InputCount = _typingTest.InputCount;
        CurrentSpeed = _typingTest.GetCurrentSpeed();
        CurrentAccuracy = _typingTest.GetCurrentAccuracy();
    }

    /// <summary>
    /// 拦截剪贴板粘贴（XAML 中 Ctrl+V/Shift+Insert 路由事件调用）。
    /// </summary>
    /// <returns>是否拦截（true 拦截，false 放行）。</returns>
    public bool ShouldBlockClipboard()
        => _protection?.ShouldBlockClipboardPaste() ?? false;

    /// <summary>
    /// 拦截右键菜单（XAML 中 ContextMenuOpening 调用）。
    /// </summary>
    public bool ShouldBlockContextMenu()
        => _protection?.ShouldBlockContextMenu() ?? false;

    /// <summary>
    /// 拦截拖拽（XAML 中 DragEnter 调用）。
    /// </summary>
    public bool ShouldBlockDragDrop()
        => _protection?.ShouldBlockDragDrop() ?? false;

    /// <summary>
    /// 主动退出考试（学生端关闭窗口或教师端停止时调用）。
    /// </summary>
    [RelayCommand]
    private void ExitExam()
    {
        _protection?.Unlock();
        _dashboardTimer?.Dispose();
        _dashboardTimer = null;
        _statusReport?.Stop();
        IsExamRunning = false;
        ExamStateText = "考试已结束";
        ExamExited?.Invoke();
    }

    private void UpdateDashboard()
    {
        if (_timeSync is null)
        {
            return;
        }
        RemainingSeconds = _timeSync.GetRemainingSeconds();
        ExamStateText = _timeSync.ExamState switch
        {
            ClientExamState.Idle => "等待开考...",
            ClientExamState.Running => "考试进行中",
            ClientExamState.Paused => "考试已暂停",
            ClientExamState.Ended => "考试已结束",
            _ => "未知状态"
        };
        IsExamRunning = _timeSync.ExamState == ClientExamState.Running;
        OnPropertyChanged(nameof(DashboardProgress));
        OnPropertyChanged(nameof(RemainingDisplay));

        if (IsExamRunning && _timeSync.HasSynced && _sessionId == Guid.Empty)
        {
            _sessionId = _timeSync.SessionId;
            // 首次进入考试：启动状态上报。
            if (_statusReport is not null && !string.IsNullOrEmpty(_studentId))
            {
                _statusReport.Start(_studentId, _sessionId, GetSnapshot);
            }
        }

        // 时间结束自动提交。
        if (_timeSync.ExamState == ClientExamState.Ended && IsExamRunning)
        {
            _ = SubmitResultAsync();
        }
    }

    private StatusReportDto GetSnapshot()
    {
        return new StatusReportDto
        {
            Speed = _typingTest.GetCurrentSpeed(),
            Accuracy = _typingTest.GetCurrentAccuracy(),
            Progress = _typingTest.InputCount,
            TotalChars = _typingTest.TotalChars
        };
    }

    private Task OnPacketReceived(MessageType type, byte[] payload)
    {
        if (type == MessageType.QuestionPush)
        {
            try
            {
                QuestionDto? q;
                using (var ms = new System.IO.MemoryStream(payload))
                {
                    q = Serializer.Deserialize<QuestionDto>(ms);
                }
                _typingTest.SetQuestion(q);
                _sessionId = q.SessionId;
                OriginalText = q.Content ?? string.Empty;
                TotalChars = q.Content?.Length ?? 0;
                TotalSeconds = q.Duration > 0 ? q.Duration : 1;
                InputText = string.Empty;
                InputCount = 0;
                RefreshChars();
                _protection?.LockForExam();
                ExamStateText = "试题已下发，准备答题...";
                _logger?.LogInformation("试题已下发：{Length} 字符", OriginalText.Length);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "试题解析失败");
                AlertText = "试题解析失败：" + ex.Message;
            }
        }
        else if (type == MessageType.TimeSync)
        {
            try
            {
                using var ms = new System.IO.MemoryStream(payload);
                var msg = Serializer.Deserialize<Network.Messages.TimeSyncMessage>(ms);
                _timeSync.OnTimeSyncReceived(msg);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "时间同步消息解析失败");
            }
        }
        else if (type == MessageType.ExamControl)
        {
            // 教师端停止/重新考试等流程控制：先标记状态，由 UpdateDashboard 处理过渡。
            if (_timeSync.ExamState == ClientExamState.Ended)
            {
                _ = SubmitResultAsync();
            }
        }
        return Task.CompletedTask;
    }

    private async Task SubmitResultAsync()
    {
        if (_resultSubmit is null)
        {
            return;
        }
        var dto = new ExamResultDto
        {
            RecordId = Guid.NewGuid(),
            SessionId = _sessionId,
            StudentId = _studentId,
            Speed = _typingTest.GetCurrentSpeed(),
            Accuracy = _typingTest.GetCurrentAccuracy(),
            Anomalies = AlertText,
            SubmittedAt = DateTime.UtcNow
        };
        try
        {
            var result = await _resultSubmit.SubmitAsync(dto).ConfigureAwait(false);
            ExamStateText = result.Success ? "成绩已提交" : "成绩回传失败：" + result.ErrorMessage;
        }
        catch (Exception ex)
        {
            ExamStateText = "成绩提交异常：" + ex.Message;
        }
        finally
        {
            _protection?.Unlock();
            _statusReport?.Stop();
            IsExamRunning = false;
        }
    }

    private void RefreshChars()
    {
        var states = _typingTest.GetCompareStates();
        Chars.Clear();
        foreach (var s in states)
        {
            Chars.Add(new CharRenderState
            {
                Character = s.Expected,
                IsEntered = s.IsEntered,
                IsCorrect = s.IsEntered && s.IsCorrect,
                IsWrong = s.IsEntered && !s.IsCorrect
            });
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
        _dashboardTimer?.Dispose();
        _dashboardTimer = null;
        if (_client is not null)
        {
            _client.PacketReceived -= OnPacketReceived;
        }
        _statusReport?.Stop();
        _protection?.Unlock();
    }
}
