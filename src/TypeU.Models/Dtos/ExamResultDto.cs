using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 考试成绩回传 DTO（学生端 → 教师端）。
/// </summary>
[ProtoContract]
public sealed partial class ExamResultDto
{
    /// <summary>
    /// 记录 ID（由学生端在收卷时生成）。
    /// </summary>
    [ProtoMember(1)]
    public Guid RecordId { get; set; }

    /// <summary>
    /// 会话 ID。
    /// </summary>
    [ProtoMember(2)]
    public Guid SessionId { get; set; }

    /// <summary>
    /// 学号。
    /// </summary>
    [ProtoMember(3)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// 最终速度（字/分钟）。
    /// </summary>
    [ProtoMember(4)]
    public double Speed { get; set; }

    /// <summary>
    /// 正确率（0-100）。
    /// </summary>
    [ProtoMember(5)]
    public double Accuracy { get; set; }

    /// <summary>
    /// 异常记录（JSON 字符串）。
    /// </summary>
    [ProtoMember(6)]
    public string Anomalies { get; set; } = string.Empty;

    /// <summary>
    /// 提交时间（UTC）。
    /// </summary>
    [ProtoMember(7)]
    public DateTime SubmittedAt { get; set; }

    /// <summary>
    /// 第几次测验（1..N）。
    /// </summary>
    [ProtoMember(8)]
    public int AttemptIndex { get; set; } = 1;
}
