using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
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
/// 教师端考试流程服务：开始/暂停/停止/重新考试 + 加密下发试题。
/// </summary>
public sealed class TeacherExamService
{
    private readonly TcpExamServer _server;
    private readonly ExamRepository _examRepository;
    private readonly QuestionRepository _questionRepository;
    private readonly MonitoringService _monitoring;
    private readonly ILogger<TeacherExamService>? _logger;
    private ExamSession? _currentSession;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    public TeacherExamService(
        TcpExamServer server,
        ExamRepository examRepository,
        QuestionRepository questionRepository,
        MonitoringService monitoring,
        ILogger<TeacherExamService>? logger = null)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _examRepository = examRepository ?? throw new ArgumentNullException(nameof(examRepository));
        _questionRepository = questionRepository ?? throw new ArgumentNullException(nameof(questionRepository));
        _monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
        _logger = logger;
    }

    /// <summary>
    /// 当前活动会话（未开始时为 null）。
    /// </summary>
    public ExamSession? CurrentSession => _currentSession;

    /// <summary>
    /// 开始考试：创建会话 + 加密下发试题 + 锁定学生端。
    /// </summary>
    public async Task StartAsync(ExamMode mode, Guid questionId, int durationSeconds)
    {
        if (_currentSession is not null)
        {
            throw new InvalidOperationException("已有进行中的考试会话，请先停止。");
        }

        var question = _questionRepository.GetById(questionId)
            ?? throw new InvalidOperationException("试题不存在。");

        var now = DateTime.UtcNow;
        _currentSession = new ExamSession
        {
            SessionId = Guid.NewGuid(),
            Mode = mode,
            QuestionId = questionId,
            StartedAt = now,
            EndedAt = DateTime.MinValue,
            Duration = durationSeconds
        };
        _examRepository.InsertSession(_currentSession);

        var questionDto = new QuestionDto
        {
            QuestionId = question.QuestionId,
            Type = question.Type,
            Content = question.Content,
            Mode = mode,
            Duration = durationSeconds,
            SessionId = _currentSession.SessionId
        };

        var payload = SerializeProto(questionDto);
        await _server.BroadcastAsync(MessageType.QuestionPush, payload).ConfigureAwait(false);

        var ctrl = new ExamControlMessage
        {
            Action = ExamControlAction.Start,
            SessionId = _currentSession.SessionId,
            QuestionId = questionId,
            Duration = durationSeconds,
            IssuedAtMs = TimestampValidator.NowUtcMs()
        };
        await _server.BroadcastAsync(MessageType.ExamControl, SerializeProto(ctrl)).ConfigureAwait(false);

        _logger?.LogInformation("开始考试：会话 {SessionId} 模式 {Mode} 时长 {Duration}s",
            _currentSession.SessionId, mode, durationSeconds);
    }

    /// <summary>
    /// 暂停考试。
    /// </summary>
    public async Task PauseAsync()
    {
        if (_currentSession is null)
        {
            return;
        }
        await SendControlAsync(ExamControlAction.Pause).ConfigureAwait(false);
        _logger?.LogInformation("考试已暂停：{SessionId}", _currentSession.SessionId);
    }

    /// <summary>
    /// 恢复考试。
    /// </summary>
    public async Task ResumeAsync()
    {
        if (_currentSession is null)
        {
            return;
        }
        await SendControlAsync(ExamControlAction.Resume).ConfigureAwait(false);
        _logger?.LogInformation("考试已恢复：{SessionId}", _currentSession.SessionId);
    }

    /// <summary>
    /// 停止考试（收卷）：广播 Stop + 关闭会话。
    /// </summary>
    public async Task StopAsync()
    {
        if (_currentSession is null)
        {
            return;
        }

        await SendControlAsync(ExamControlAction.Stop).ConfigureAwait(false);

        _currentSession.EndedAt = DateTime.UtcNow;
        _examRepository.UpdateSession(_currentSession);
        _logger?.LogInformation("考试已停止：{SessionId}", _currentSession.SessionId);
        _currentSession = null;
    }

    /// <summary>
    /// 重新考试：广播 Restart（学生端清空草稿、重置状态），监控看板清零，重新下发试题。
    /// </summary>
    public async Task RestartAsync()
    {
        if (_currentSession is null)
        {
            throw new InvalidOperationException("无活动考试会话，无法重新考试。");
        }

        await SendControlAsync(ExamControlAction.Restart).ConfigureAwait(false);
        _monitoring.ResetAll();

        var question = _questionRepository.GetById(_currentSession.QuestionId);
        if (question is null)
        {
            throw new InvalidOperationException("试题已被删除，无法重新考试。");
        }

        var questionDto = new QuestionDto
        {
            QuestionId = question.QuestionId,
            Type = question.Type,
            Content = question.Content,
            Mode = _currentSession.Mode,
            Duration = _currentSession.Duration,
            SessionId = _currentSession.SessionId
        };
        await _server.BroadcastAsync(MessageType.QuestionPush, SerializeProto(questionDto)).ConfigureAwait(false);
        await SendControlAsync(ExamControlAction.Start).ConfigureAwait(false);

        _logger?.LogInformation("考试已重新开始：{SessionId}", _currentSession.SessionId);
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
