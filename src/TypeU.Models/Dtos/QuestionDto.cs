using System;
using Larpx.PersonalTools.TypeU.Models.Enums;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 试题下发 DTO（教师端 → 学生端）。
/// </summary>
[ProtoContract]
public sealed partial class QuestionDto
{
    /// <summary>
    /// 试题 ID。
    /// </summary>
    [ProtoMember(1)]
    public Guid QuestionId { get; set; }

    /// <summary>
    /// 试题类型。
    /// </summary>
    [ProtoMember(2)]
    public QuestionType Type { get; set; }

    /// <summary>
    /// 试题正文。纠错模式下为含错原文。
    /// </summary>
    [ProtoMember(3)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 考试模式。
    /// </summary>
    [ProtoMember(4)]
    public ExamMode Mode { get; set; }

    /// <summary>
    /// 时长（秒）。
    /// </summary>
    [ProtoMember(5)]
    public int Duration { get; set; }

    /// <summary>
    /// 会话 ID。
    /// </summary>
    [ProtoMember(6)]
    public Guid SessionId { get; set; }

    /// <summary>
    /// 参考答案。纠错模式下为正确文本；其它模式可为空。
    /// </summary>
    [ProtoMember(7)]
    public string ExpectedContent { get; set; } = string.Empty;

    /// <summary>
    /// 允许的测验次数（1–5）。
    /// </summary>
    [ProtoMember(8)]
    public int MaxAttempts { get; set; } = 1;

    /// <summary>
    /// 交齐成绩后是否允许自由练习。
    /// </summary>
    [ProtoMember(9)]
    public bool AllowPracticeAfterSubmit { get; set; }
}
