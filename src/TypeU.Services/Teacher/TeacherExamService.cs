using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Network.Messages;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 教师端考试流程：开考持久化、结束不强制登出、恢复未结束会话。
/// </summary>
public sealed class TeacherExamService
{
    private readonly TcpExamServer _server;
    private readonly ExamRepository _examRepository;
    private readonly QuestionRepository _questionRepository;
    private readonly MonitoringService _monitoring;
    private readonly TimeSyncService _timeSync;
    private readonly ILogger<TeacherExamService>? _logger;
    private ExamSession? _currentSession;

    /// <summary>
    /// 教师工号（启动时录入）。
    /// </summary>
    public string TeacherId { get; set; } = string.Empty;

    /// <summary>
    /// 教师姓名（启动时录入）。
    /// </summary>
    public string TeacherName { get; set; } = string.Empty;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    public TeacherExamService(
        TcpExamServer server,
        ExamRepository examRepository,
        QuestionRepository questionRepository,
        MonitoringService monitoring,
        TimeSyncService timeSync,
        ILogger<TeacherExamService>? logger = null)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _examRepository = examRepository ?? throw new ArgumentNullException(nameof(examRepository));
        _questionRepository = questionRepository ?? throw new ArgumentNullException(nameof(questionRepository));
        _monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
        _timeSync = timeSync ?? throw new ArgumentNullException(nameof(timeSync));
        _logger = logger;
    }

    /// <summary>
    /// 当前活动会话。
    /// </summary>
    public ExamSession? CurrentSession => _currentSession;

    /// <summary>
    /// 是否存在进行中的考试。
    /// </summary>
    public bool IsExamRunning => _currentSession is { Status: ExamSessionStatus.Running };

    /// <summary>
    /// 从数据库恢复未结束会话（教师端重启后调用）。
    /// </summary>
    public void TryRestoreRunningSession()
    {
        var running = _examRepository.GetRunningSession();
        if (running is null)
        {
            return;
        }

        _currentSession = running;
        if (!string.IsNullOrEmpty(running.TeacherId))
        {
            TeacherId = running.TeacherId;
        }

        if (!string.IsNullOrEmpty(running.TeacherName))
        {
            TeacherName = running.TeacherName;
        }

        _logger?.LogInformation("已恢复未结束考试会话 {SessionId}", running.SessionId);
    }

    /// <summary>
    /// 开始考试并持久化。
    /// </summary>
    public async Task StartAsync(
        ExamMode mode,
        Guid questionId,
        int durationSeconds,
        int maxAttempts,
        bool allowPracticeAfterSubmit)
    {
        if (_currentSession is { Status: ExamSessionStatus.Running })
        {
            throw new InvalidOperationException("已有进行中的考试会话，请先结束。");
        }

        // 清理已结束会话引用，开启新会话。
        _currentSession = null;

        if (maxAttempts < 1 || maxAttempts > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "重考次数须为 1–5。");
        }

        var question = _questionRepository.GetById(questionId)
            ?? throw new InvalidOperationException("试题不存在。");

        if (mode == ExamMode.ErrorCorrection && string.IsNullOrWhiteSpace(question.ExpectedContent))
        {
            throw new InvalidOperationException("纠错模式试题必须填写参考答案。");
        }

        var now = DateTime.UtcNow;
        _currentSession = new ExamSession
        {
            SessionId = Guid.NewGuid(),
            Mode = mode,
            QuestionId = questionId,
            StartedAt = now,
            EndedAt = DateTime.MinValue,
            Duration = durationSeconds,
            MaxAttempts = maxAttempts,
            AllowPracticeAfterSubmit = allowPracticeAfterSubmit,
            TeacherId = TeacherId,
            TeacherName = TeacherName,
            Status = ExamSessionStatus.Running
        };
        _examRepository.InsertSession(_currentSession);

        var questionDto = new QuestionDto
        {
            QuestionId = question.QuestionId,
            Type = question.Type,
            Content = question.Content,
            ExpectedContent = question.ExpectedContent,
            Mode = mode,
            Duration = durationSeconds,
            SessionId = _currentSession.SessionId,
            MaxAttempts = maxAttempts,
            AllowPracticeAfterSubmit = allowPracticeAfterSubmit
        };

        await _server.BroadcastAsync(MessageType.QuestionPush, SerializeProto(questionDto)).ConfigureAwait(false);

        var life = new ExamLifecycleDto
        {
            SessionId = _currentSession.SessionId,
            Started = true,
            MaxAttempts = maxAttempts,
            AllowPracticeAfterSubmit = allowPracticeAfterSubmit,
            Mode = mode,
            Duration = durationSeconds,
            Message = "考试已开始，请登录后作答。"
        };
        await _server.BroadcastAsync(MessageType.ExamLifecycle, SerializeProto(life)).ConfigureAwait(false);

        var ctrl = new ExamControlMessage
        {
            Action = ExamControlAction.Start,
            SessionId = _currentSession.SessionId,
            QuestionId = questionId,
            Duration = durationSeconds,
            IssuedAtMs = TimestampValidator.NowUtcMs()
        };
        await _server.BroadcastAsync(MessageType.ExamControl, SerializeProto(ctrl)).ConfigureAwait(false);

        _logger?.LogInformation("开始考试：{SessionId} 模式 {Mode} 次数 {Attempts}",
            _currentSession.SessionId, mode, maxAttempts);

        // 联动时间同步服务：广播考试进行中与剩余时长，供学生端倒计时基准使用。
        _timeSync.NotifyExamStarted(_currentSession.SessionId, durationSeconds);
    }

    /// <summary>
    /// 暂停考试。
    /// </summary>
    public async Task PauseAsync()
    {
        if (_currentSession is null || _currentSession.Status != ExamSessionStatus.Running)
        {
            return;
        }

        await SendControlAsync(ExamControlAction.Pause).ConfigureAwait(false);
        _timeSync.NotifyExamPaused();
    }

    /// <summary>
    /// 恢复考试。
    /// </summary>
    public async Task ResumeAsync()
    {
        if (_currentSession is null || _currentSession.Status != ExamSessionStatus.Running)
        {
            return;
        }

        await SendControlAsync(ExamControlAction.Resume).ConfigureAwait(false);
        _timeSync.NotifyExamResumed();
    }

    /// <summary>
    /// 结束考试：持久化 Ended，广播可自行登出（不强制 Logout）。
    /// </summary>
    public async Task StopAsync()
    {
        if (_currentSession is null)
        {
            return;
        }

        await SendControlAsync(ExamControlAction.Stop).ConfigureAwait(false);

        _currentSession.EndedAt = DateTime.UtcNow;
        _currentSession.Status = ExamSessionStatus.Ended;
        _examRepository.UpdateSession(_currentSession);

        var life = new ExamLifecycleDto
        {
            SessionId = _currentSession.SessionId,
            Started = false,
            MaxAttempts = _currentSession.MaxAttempts,
            AllowPracticeAfterSubmit = _currentSession.AllowPracticeAfterSubmit,
            Mode = _currentSession.Mode,
            Duration = _currentSession.Duration,
            Message = "考试已结束，可自行登出以清除本机个人信息。"
        };
        await _server.BroadcastAsync(MessageType.ExamLifecycle, SerializeProto(life)).ConfigureAwait(false);

        _logger?.LogInformation("考试已结束：{SessionId}", _currentSession.SessionId);
        // 保留会话引用以便允许登出 / 成绩查询；下次 Start 前会覆盖。
        _timeSync.NotifyExamStopped();
    }

    /// <summary>
    /// 重新考试：重置监控并重新下发试题。
    /// </summary>
    public async Task RestartAsync()
    {
        if (_currentSession is null || _currentSession.Status != ExamSessionStatus.Running)
        {
            throw new InvalidOperationException("无活动考试会话，无法重新考试。");
        }

        await SendControlAsync(ExamControlAction.Restart).ConfigureAwait(false);
        _monitoring.ResetAll();

        var question = _questionRepository.GetById(_currentSession.QuestionId)
            ?? throw new InvalidOperationException("试题已被删除，无法重新考试。");

        var questionDto = new QuestionDto
        {
            QuestionId = question.QuestionId,
            Type = question.Type,
            Content = question.Content,
            ExpectedContent = question.ExpectedContent,
            Mode = _currentSession.Mode,
            Duration = _currentSession.Duration,
            SessionId = _currentSession.SessionId,
            MaxAttempts = _currentSession.MaxAttempts,
            AllowPracticeAfterSubmit = _currentSession.AllowPracticeAfterSubmit
        };
        await _server.BroadcastAsync(MessageType.QuestionPush, SerializeProto(questionDto)).ConfigureAwait(false);
        await SendControlAsync(ExamControlAction.Start).ConfigureAwait(false);
    }

    private async Task SendControlAsync(ExamControlAction action)
    {
        if (_currentSession is null)
        {
            return;
        }

        var ctrl = new ExamControlMessage
        {
            Action = action,
            SessionId = _currentSession.SessionId,
            QuestionId = _currentSession.QuestionId,
            Duration = _currentSession.Duration,
            IssuedAtMs = TimestampValidator.NowUtcMs()
        };
        await _server.BroadcastAsync(MessageType.ExamControl, SerializeProto(ctrl)).ConfigureAwait(false);
    }

    private static byte[] SerializeProto<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T msg)
        where T : class
    {
        using var ms = new System.IO.MemoryStream();
        Serializer.Serialize(ms, msg);
        return ms.ToArray();
    }
}
