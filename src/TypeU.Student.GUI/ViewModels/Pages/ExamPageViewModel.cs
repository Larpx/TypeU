using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Core.AntiCheat;
using Larpx.PersonalTools.TypeU.Core.Devices;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Enums;
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

    /// <summary>是否正确（已输入且与比对基准一致）。</summary>
    public bool IsCorrect { get; init; }

    /// <summary>是否错误（已输入且与比对基准不一致）。</summary>
    public bool IsWrong { get; init; }
}

/// <summary>
/// 带行号的原文行（代码编辑器风格）。
/// </summary>
public sealed class LineRenderState
{
    /// <summary>行号（从 1 开始）。</summary>
    public int LineNumber { get; init; }

    /// <summary>行号显示文本。</summary>
    public string LineNumberText => LineNumber.ToString().PadLeft(3, ' ');

    /// <summary>该行字符列表。</summary>
    public ObservableCollection<CharRenderState> Chars { get; } = new();
}

/// <summary>
/// 考试页 ViewModel：沉浸式原文/输入/高亮/仪表盘/防粘贴；多试次与登出锁定。
/// </summary>
public sealed partial class ExamPageViewModel : ViewModelBase, IDisposable
{
    private readonly TypingTestService _typingTest;
    private readonly ClientTimeSyncService _timeSync;
    private readonly StatusReportService _statusReport;
    private readonly ResultSubmitService _resultSubmit;
    private readonly TcpExamClient _client;
    private readonly StudentConnectionService? _connection;
    private readonly DeviceFingerprintProvider? _fingerprint;
    private readonly AntiCheatMonitor _antiCheat;
    private readonly InputProtectionPolicy _protection;
    private readonly ILogger<ExamPageViewModel>? _logger;
    private Timer? _dashboardTimer;
    private string _studentId = string.Empty;
    private Guid _sessionId;
    private bool _disposed;
    private bool _isOfflineMode;
    private long _offlineEndTickMs;
    private bool _offlineSubmitted;
    private int _attemptIndex;
    private int _maxAttempts = 1;
    private bool _allowPracticeAfterSubmit;
    private bool _logoutLocked;
    private bool _examSessionEnded;
    private QuestionDto? _lastQuestion;
    private bool _submitting;

    private const string OfflinePracticeText =
        "单机练习：熟练的打字技能是信息时代的基本素养。请专注输入，注意正确率与节奏，避免依赖复制粘贴。";

    private const int OfflineDurationSeconds = 300;

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
        PromptTitle = "原文";
        var designLine = new LineRenderState { LineNumber = 1 };
        foreach (var c in OriginalText)
        {
            designLine.Chars.Add(new CharRenderState { Character = c });
            Chars.Add(new CharRenderState { Character = c });
        }
        Lines.Add(designLine);
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
        StudentConnectionService connection,
        DeviceFingerprintProvider fingerprint,
        ILogger<ExamPageViewModel>? logger = null)
    {
        _typingTest = typingTest ?? throw new ArgumentNullException(nameof(typingTest));
        _timeSync = timeSync ?? throw new ArgumentNullException(nameof(timeSync));
        _statusReport = statusReport ?? throw new ArgumentNullException(nameof(statusReport));
        _resultSubmit = resultSubmit ?? throw new ArgumentNullException(nameof(resultSubmit));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _antiCheat = antiCheat ?? throw new ArgumentNullException(nameof(antiCheat));
        _protection = protection ?? throw new ArgumentNullException(nameof(protection));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
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
    /// 字符渲染列表（比对高亮；非纠错模式叠在原文上）。
    /// </summary>
    public ObservableCollection<CharRenderState> Chars { get; } = new();

    /// <summary>
    /// 纠错模式输入校对条：仅展示已输入字符的对错色，不泄露未输入参考答案。
    /// </summary>
    public ObservableCollection<CharRenderState> InputHighlightChars { get; } = new();

    /// <summary>
    /// 带行号的展示行（原文 / 含错原文）。
    /// </summary>
    public ObservableCollection<LineRenderState> Lines { get; } = new();

    /// <summary>
    /// 原文区标题。
    /// </summary>
    [ObservableProperty]
    private string _promptTitle = "原文";

    /// <summary>
    /// 是否为纠错模式（含错原文只读展示，输入比对参考答案）。
    /// </summary>
    [ObservableProperty]
    private bool _isErrorCorrectionMode;

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
    /// 是否可再考一次。
    /// </summary>
    [ObservableProperty]
    private bool _canRetryAttempt;

    /// <summary>
    /// 试次提示。
    /// </summary>
    [ObservableProperty]
    private string _attemptHint = string.Empty;

    /// <summary>
    /// 退出考试事件（MainWindowViewModel 监听以切回登录页）。
    /// </summary>
    public event Action? ExamExited;

    /// <summary>
    /// 考试/练习会话解锁事件（结束沉浸式窗口约束，仍可停留在考试页查看成绩）。
    /// </summary>
    public event Action? SessionUnlocked;

    /// <summary>
    /// 登出锁定状态变更（true=锁定不可退出）。
    /// </summary>
    public event Action<bool>? LogoutLockChanged;

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
        _isOfflineMode = false;
        _offlineSubmitted = false;
        _studentId = studentId ?? string.Empty;
        _sessionId = sessionId;
    }

    /// <summary>
    /// 联网考试会话开始：锁定退出，等待试题下发。
    /// </summary>
    public void BeginOnlineExamSession(string studentId, int maxAttempts, bool allowPracticeAfterSubmit)
    {
        _isOfflineMode = false;
        _offlineSubmitted = false;
        _submitting = false;
        _examSessionEnded = false;
        _studentId = studentId ?? string.Empty;
        _maxAttempts = maxAttempts > 0 ? maxAttempts : 1;
        _allowPracticeAfterSubmit = allowPracticeAfterSubmit;
        _attemptIndex = 0;
        _logoutLocked = true;
        CanRetryAttempt = false;
        AttemptHint = $"最多 {_maxAttempts} 次测验";
        ExamStateText = "已登录，等待试题下发...";
        IsExamRunning = false;
        LogoutLockChanged?.Invoke(true);
        StartDashboard();
        _logger?.LogInformation("联网考试会话：学号 {Id}，次数 {N}", _studentId, _maxAttempts);
    }

    /// <summary>
    /// 启动单机练习：内置范文 + 本地倒计时，不上报/不回传。
    /// </summary>
    /// <param name="studentId">学号（本地展示用）。</param>
    public void StartOfflinePractice(string studentId)
    {
        _isOfflineMode = true;
        _offlineSubmitted = false;
        _logoutLocked = false;
        _examSessionEnded = false;
        _studentId = studentId ?? string.Empty;
        _sessionId = Guid.NewGuid();
        _antiCheat.Reset();
        CanRetryAttempt = false;

        var question = new QuestionDto
        {
            QuestionId = Guid.NewGuid(),
            Type = QuestionType.Chinese,
            Content = OfflinePracticeText,
            ExpectedContent = string.Empty,
            Mode = ExamMode.TimedSprint,
            Duration = OfflineDurationSeconds,
            SessionId = _sessionId
        };

        ApplyQuestion(question, "原文（单机练习）");
        ExamStateText = "单机练习进行中";
        IsExamRunning = true;
        _offlineEndTickMs = Environment.TickCount64 + OfflineDurationSeconds * 1000L;
        _protection.LockForExam();
        StartDashboard();
        _logger?.LogInformation("已启动单机练习：学号 {StudentId}，时长 {Sec}s", _studentId, OfflineDurationSeconds);
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
    /// 主动退出考试（仅未锁定时可调用）。
    /// </summary>
    [RelayCommand]
    private void ExitExam()
    {
        if (_logoutLocked && !_examSessionEnded)
        {
            AlertText = "考试进行中不可退出，请等待教师结束考试或允许登出。";
            return;
        }

        _protection?.Unlock();
        _dashboardTimer?.Dispose();
        _dashboardTimer = null;
        _statusReport?.Stop();
        IsExamRunning = false;
        ExamStateText = "考试已结束";
        ExamExited?.Invoke();
    }

    /// <summary>
    /// 再考一次（同一试题，本地重置）。
    /// </summary>
    [RelayCommand]
    private void RetryAttempt()
    {
        if (!CanRetryAttempt || _lastQuestion is null || _examSessionEnded)
        {
            return;
        }

        CanRetryAttempt = false;
        _offlineSubmitted = false;
        _submitting = false;
        _antiCheat.Reset();
        ApplyQuestion(_lastQuestion, PromptTitle);
        ExamStateText = $"第 {_attemptIndex + 1} 次测验进行中";
        IsExamRunning = true;
        _protection?.LockForExam();
        if (_statusReport is not null && !string.IsNullOrEmpty(_studentId) && _sessionId != Guid.Empty)
        {
            _statusReport.Start(_studentId, _sessionId, GetSnapshot);
        }
    }

    /// <summary>
    /// 向教师端请求登出并清除本机会话上下文。
    /// </summary>
    public async Task RequestLogoutAsync()
    {
        if (_connection is null || _fingerprint is null)
        {
            return;
        }

        try
        {
            await _connection.LogoutAsync(new LogoutDto
            {
                StudentId = _studentId,
                DeviceFingerprint = _fingerprint.GetFingerprint(),
                SessionId = _sessionId
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "登出上报失败");
        }
        finally
        {
            _logoutLocked = false;
            LogoutLockChanged?.Invoke(false);
            _protection?.Unlock();
            _statusReport?.Stop();
        }
    }

    private void UpdateDashboard()
    {
        if (_isOfflineMode)
        {
            UpdateOfflineDashboard();
            return;
        }

        if (_timeSync is null)
        {
            return;
        }

        RemainingSeconds = _timeSync.GetRemainingSeconds();
        if (!_examSessionEnded)
        {
            ExamStateText = _timeSync.ExamState switch
            {
                ClientExamState.Idle => string.IsNullOrEmpty(OriginalText) ? "已登录，等待试题下发..." : "准备中...",
                ClientExamState.Running => $"考试进行中（第 {_attemptIndex + 1} / {_maxAttempts} 次）",
                ClientExamState.Paused => "考试已暂停",
                ClientExamState.Ended => ExamStateText,
                _ => "未知状态"
            };
            IsExamRunning = _timeSync.ExamState == ClientExamState.Running;
        }

        OnPropertyChanged(nameof(DashboardProgress));
        OnPropertyChanged(nameof(RemainingDisplay));

        if (IsExamRunning && _timeSync.HasSynced && _sessionId == Guid.Empty)
        {
            _sessionId = _timeSync.SessionId;
            if (_statusReport is not null && !string.IsNullOrEmpty(_studentId))
            {
                _statusReport.Start(_studentId, _sessionId, GetSnapshot);
            }
        }

        if (_timeSync.ExamState == ClientExamState.Ended && IsExamRunning && !_submitting)
        {
            _ = SubmitResultAsync();
        }
    }

    private void UpdateOfflineDashboard()
    {
        var remainMs = _offlineEndTickMs - Environment.TickCount64;
        RemainingSeconds = (int)Math.Max(0, remainMs / 1000);
        ExamStateText = RemainingSeconds > 0 ? "单机练习进行中" : "单机练习已结束";
        IsExamRunning = RemainingSeconds > 0;
        OnPropertyChanged(nameof(DashboardProgress));
        OnPropertyChanged(nameof(RemainingDisplay));

        if (!_offlineSubmitted && RemainingSeconds <= 0)
        {
            _ = SubmitResultAsync();
        }
    }

    private bool _isWindowMinimized;

    private StatusReportDto GetSnapshot()
    {
        return new StatusReportDto
        {
            Speed = _typingTest.GetCurrentSpeed(),
            Accuracy = _typingTest.GetCurrentAccuracy(),
            Progress = _typingTest.InputCount,
            TotalChars = _typingTest.TotalChars,
            IsMinimized = _isWindowMinimized,
            ClientMode = _isOfflineMode
                ? "单机"
                : (_logoutLocked ? "考试中" : "在线"),
            DeviceFingerprint = _fingerprint?.GetFingerprint() ?? string.Empty
        };
    }

    /// <summary>
    /// 由主窗口同步最小化状态。
    /// </summary>
    public void SetWindowMinimized(bool minimized) => _isWindowMinimized = minimized;

    private Task OnPacketReceived(MessageType type, byte[] payload)
    {
        if (_isOfflineMode)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (type == MessageType.QuestionPush)
            {
                QuestionDto? q;
                using (var ms = new System.IO.MemoryStream(payload))
                {
                    q = Serializer.Deserialize<QuestionDto>(ms);
                }

                if (q.MaxAttempts > 0)
                {
                    _maxAttempts = q.MaxAttempts;
                }

                _allowPracticeAfterSubmit = q.AllowPracticeAfterSubmit;
                _sessionId = q.SessionId;
                _attemptIndex = 0;
                _offlineSubmitted = false;
                _submitting = false;
                CanRetryAttempt = false;
                ApplyQuestion(q, q.Mode == ExamMode.ErrorCorrection
                    ? "含错原文（请在下方输入修正后的正确文本）"
                    : "原文");
                ExamStateText = IsErrorCorrectionMode ? "纠错试题已下发，准备修正..." : "试题已下发，准备答题...";
                _protection?.LockForExam();
                if (_statusReport is not null && !string.IsNullOrEmpty(_studentId))
                {
                    _statusReport.Start(_studentId, _sessionId, GetSnapshot);
                }

                _logger?.LogInformation("试题已下发：展示 {Display} 字，比对 {Compare} 字",
                    OriginalText.Length, TotalChars);
            }
            else if (type == MessageType.TimeSync)
            {
                using var ms = new System.IO.MemoryStream(payload);
                var msg = Serializer.Deserialize<Network.Messages.TimeSyncMessage>(ms);
                _timeSync.OnTimeSyncReceived(msg);
            }
            else if (type == MessageType.ExamControl)
            {
                if (_timeSync.ExamState == ClientExamState.Ended && !_submitting)
                {
                    _ = SubmitResultAsync();
                }
            }
            else if (type == MessageType.ExamLifecycle)
            {
                using var ms = new System.IO.MemoryStream(payload);
                var life = Serializer.Deserialize<ExamLifecycleDto>(ms);
                if (life.Started)
                {
                    _maxAttempts = life.MaxAttempts > 0 ? life.MaxAttempts : _maxAttempts;
                    _allowPracticeAfterSubmit = life.AllowPracticeAfterSubmit;
                    _examSessionEnded = false;
                    _logoutLocked = true;
                    LogoutLockChanged?.Invoke(true);
                }
                else
                {
                    HandleExamEnded(life.Message);
                }
            }
            else if (type == MessageType.LogoutAllow)
            {
                using var ms = new System.IO.MemoryStream(payload);
                var allow = Serializer.Deserialize<LogoutAllowDto>(ms);
                if (allow.Allowed
                    && (string.IsNullOrEmpty(allow.StudentId) || allow.StudentId == _studentId))
                {
                    _logoutLocked = false;
                    LogoutLockChanged?.Invoke(false);
                    ExamStateText = "教师已允许登出，可清除本机信息后返回。";
                    SessionUnlocked?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "考试页报文处理失败：{Type}", type);
            AlertText = "报文处理失败：" + ex.Message;
        }

        return Task.CompletedTask;
    }

    private void HandleExamEnded(string message)
    {
        _examSessionEnded = true;
        _logoutLocked = false;
        CanRetryAttempt = false;
        IsExamRunning = false;
        _protection?.Unlock();
        _statusReport?.Stop();
        ExamStateText = string.IsNullOrEmpty(message)
            ? "考试已结束，可自行选择登出并清除本机信息。"
            : message;
        LogoutLockChanged?.Invoke(false);
        SessionUnlocked?.Invoke();
    }

    private async Task SubmitResultAsync()
    {
        if (_offlineSubmitted || _submitting)
        {
            return;
        }

        _submitting = true;
        _offlineSubmitted = true;
        var speed = _typingTest.GetCurrentSpeed();
        var accuracy = _typingTest.GetCurrentAccuracy();

        if (_isOfflineMode)
        {
            ExamStateText = $"单机练习结束：速度 {speed:F1} 字/分，正确率 {accuracy:F1}%（可关闭窗口或返回）";
            _protection?.Unlock();
            IsExamRunning = false;
            SessionUnlocked?.Invoke();
            _submitting = false;
            return;
        }

        _attemptIndex++;
        AttemptHint = $"已完成 {_attemptIndex} / {_maxAttempts} 次";
        if (_resultSubmit is null)
        {
            _submitting = false;
            return;
        }

        var dto = new ExamResultDto
        {
            RecordId = Guid.NewGuid(),
            SessionId = _sessionId,
            StudentId = _studentId,
            Speed = speed,
            Accuracy = accuracy,
            Anomalies = AlertText,
            SubmittedAt = DateTime.UtcNow,
            AttemptIndex = _attemptIndex
        };
        try
        {
            var result = await _resultSubmit.SubmitAsync(dto).ConfigureAwait(false);
            ExamStateText = result.Success
                ? $"第 {_attemptIndex} 次成绩已提交：{speed:F1} 字/分，{accuracy:F1}%"
                : "成绩回传失败：" + result.ErrorMessage;
        }
        catch (Exception ex)
        {
            ExamStateText = "成绩提交异常：" + ex.Message;
        }
        finally
        {
            IsExamRunning = false;
            _statusReport?.Stop();
            _protection?.Unlock();
            _submitting = false;

            if (!_examSessionEnded && _attemptIndex < _maxAttempts && _lastQuestion is not null)
            {
                CanRetryAttempt = true;
                ExamStateText += "（可再考一次）";
            }
            else if (!_examSessionEnded && _allowPracticeAfterSubmit)
            {
                ExamStateText += "（次数已用尽，可自由练习，仍不可登出）";
            }
            else if (!_examSessionEnded)
            {
                ExamStateText += "（次数已用尽，请等待教师结束考试）";
            }
        }
    }

    private void ApplyQuestion(QuestionDto q, string promptTitle)
    {
        _lastQuestion = q;
        _typingTest.SetQuestion(q);
        IsErrorCorrectionMode = _typingTest.IsErrorCorrection;
        PromptTitle = promptTitle;
        OriginalText = q.Content ?? string.Empty;
        TotalChars = _typingTest.TotalChars;
        TotalSeconds = q.Duration > 0 ? q.Duration : 1;
        RemainingSeconds = TotalSeconds;
        InputText = string.Empty;
        InputCount = 0;
        AlertText = string.Empty;
        RefreshChars();
    }

    private void RefreshChars()
    {
        var states = _typingTest.GetCompareStates();
        Chars.Clear();
        InputHighlightChars.Clear();
        foreach (var s in states)
        {
            Chars.Add(new CharRenderState
            {
                Character = s.Expected,
                IsEntered = s.IsEntered,
                IsCorrect = s.IsEntered && s.IsCorrect,
                IsWrong = s.IsEntered && !s.IsCorrect
            });

            if (s.IsEntered && s.Actual is { } actual)
            {
                InputHighlightChars.Add(new CharRenderState
                {
                    Character = actual,
                    IsEntered = true,
                    IsCorrect = s.IsCorrect,
                    IsWrong = !s.IsCorrect
                });
            }
        }

        RebuildLines();
    }

    /// <summary>
    /// 按换行拆分展示原文，并附行号；非纠错模式叠加入高亮状态。
    /// </summary>
    private void RebuildLines()
    {
        Lines.Clear();
        var display = OriginalText ?? string.Empty;
        var displayLines = display.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var compareIndex = 0;

        for (var i = 0; i < displayLines.Length; i++)
        {
            var line = new LineRenderState { LineNumber = i + 1 };
            var text = displayLines[i];
            for (var j = 0; j < text.Length; j++)
            {
                var ch = text[j];
                if (IsErrorCorrectionMode)
                {
                    // 纠错模式：含错原文不叠加对错色，避免泄露答案。
                    line.Chars.Add(new CharRenderState { Character = ch });
                }
                else if (compareIndex < Chars.Count)
                {
                    var src = Chars[compareIndex];
                    line.Chars.Add(new CharRenderState
                    {
                        Character = ch,
                        IsEntered = src.IsEntered,
                        IsCorrect = src.IsCorrect,
                        IsWrong = src.IsWrong
                    });
                    compareIndex++;
                }
                else
                {
                    line.Chars.Add(new CharRenderState { Character = ch });
                }
            }

            // 非纠错：换行符在比对基准中占 1 位时同步推进索引。
            if (!IsErrorCorrectionMode && i < displayLines.Length - 1 && compareIndex < Chars.Count)
            {
                compareIndex++;
            }

            Lines.Add(line);
        }

        if (Lines.Count == 0)
        {
            Lines.Add(new LineRenderState { LineNumber = 1 });
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
