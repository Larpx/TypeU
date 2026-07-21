using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Network.Messages;

/// <summary>
/// 考试流程控制消息（教师端 → 学生端）。
/// </summary>
[ProtoContract]
public sealed partial class ExamControlMessage
{
    /// <summary>
    /// 控制动作。
    /// </summary>
    [ProtoMember(1)]
    public ExamControlAction Action { get; set; }

    /// <summary>
    /// 会话 ID（开始考试时由教师端生成）。
    /// </summary>
    [ProtoMember(2)]
    public Guid SessionId { get; set; }

    /// <summary>
    /// 试题 ID（开始考试时携带）。
    /// </summary>
    [ProtoMember(3)]
    public Guid QuestionId { get; set; }

    /// <summary>
    /// 考试时长（秒）。
    /// </summary>
    [ProtoMember(4)]
    public int Duration { get; set; }

    /// <summary>
    /// 时间戳（UTC 毫秒，由教师端时间同步决定）。
    /// </summary>
    [ProtoMember(5)]
    public long IssuedAtMs { get; set; }
}
