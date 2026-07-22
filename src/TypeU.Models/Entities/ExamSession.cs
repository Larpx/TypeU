using System;
using Larpx.PersonalTools.TypeU.Models.Enums;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Entities;

/// <summary>
/// 考试会话实体（对应 ExamSessions 表）。
/// </summary>
[ProtoContract]
public sealed partial class ExamSession
{
    /// <summary>会话 ID（主键）。</summary>
    [ProtoMember(1)]
    public Guid SessionId { get; set; }

    /// <summary>考试模式。</summary>
    [ProtoMember(2)]
    public ExamMode Mode { get; set; }

    /// <summary>关联的试题 ID。</summary>
    [ProtoMember(3)]
    public Guid QuestionId { get; set; }

    /// <summary>开始时间（UTC）。</summary>
    [ProtoMember(4)]
    public DateTime StartedAt { get; set; }

    /// <summary>结束时间（UTC）；未结束为 MinValue。</summary>
    [ProtoMember(5)]
    public DateTime EndedAt { get; set; }

    /// <summary>时长（秒）。</summary>
    [ProtoMember(6)]
    public int Duration { get; set; }

    /// <summary>允许的测验次数（1–5）。</summary>
    [ProtoMember(7)]
    public int MaxAttempts { get; set; } = 1;

    /// <summary>交齐成绩后是否允许自由练习。</summary>
    [ProtoMember(8)]
    public bool AllowPracticeAfterSubmit { get; set; }

    /// <summary>教师工号。</summary>
    [ProtoMember(9)]
    public string TeacherId { get; set; } = string.Empty;

    /// <summary>教师姓名。</summary>
    [ProtoMember(10)]
    public string TeacherName { get; set; } = string.Empty;

    /// <summary>会话状态。</summary>
    [ProtoMember(11)]
    public ExamSessionStatus Status { get; set; } = ExamSessionStatus.Running;
}
