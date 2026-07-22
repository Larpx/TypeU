using System;
using Larpx.PersonalTools.TypeU.Models.Enums;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 开考/结束考试广播载荷。
/// </summary>
[ProtoContract]
public sealed partial class ExamLifecycleDto
{
    /// <summary>会话 ID。</summary>
    [ProtoMember(1)]
    public Guid SessionId { get; set; }

    /// <summary>是否为开考（false 表示结束）。</summary>
    [ProtoMember(2)]
    public bool Started { get; set; }

    /// <summary>最大测验次数。</summary>
    [ProtoMember(3)]
    public int MaxAttempts { get; set; }

    /// <summary>交卷后是否允许自由练习。</summary>
    [ProtoMember(4)]
    public bool AllowPracticeAfterSubmit { get; set; }

    /// <summary>考试模式。</summary>
    [ProtoMember(5)]
    public ExamMode Mode { get; set; }

    /// <summary>时长（秒）。</summary>
    [ProtoMember(6)]
    public int Duration { get; set; }

    /// <summary>提示文案。</summary>
    [ProtoMember(7)]
    public string Message { get; set; } = string.Empty;
}
