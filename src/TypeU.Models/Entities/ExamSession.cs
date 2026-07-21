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
    /// <summary>
    /// 会话 ID（主键）。
    /// </summary>
    [ProtoMember(1)]
    public Guid SessionId { get; set; }

    /// <summary>
    /// 考试模式。
    /// </summary>
    [ProtoMember(2)]
    public ExamMode Mode { get; set; }

    /// <summary>
    /// 关联的试题 ID。
    /// </summary>
    [ProtoMember(3)]
    public Guid QuestionId { get; set; }

    /// <summary>
    /// 开始时间（UTC）。
    /// </summary>
    [ProtoMember(4)]
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 结束时间（UTC）。
    /// </summary>
    [ProtoMember(5)]
    public DateTime EndedAt { get; set; }

    /// <summary>
    /// 时长（秒）。
    /// </summary>
    [ProtoMember(6)]
    public int Duration { get; set; }
}
