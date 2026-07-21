using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 学生实时状态上报 DTO（1-2 秒上报一次）。
/// </summary>
[ProtoContract]
public sealed partial class StatusReportDto
{
    /// <summary>
    /// 学号。
    /// </summary>
    [ProtoMember(1)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// 会话 ID。
    /// </summary>
    [ProtoMember(2)]
    public Guid SessionId { get; set; }

    /// <summary>
    /// 当前速度（字/分钟）。
    /// </summary>
    [ProtoMember(3)]
    public double Speed { get; set; }

    /// <summary>
    /// 当前正确率（0-100）。
    /// </summary>
    [ProtoMember(4)]
    public double Accuracy { get; set; }

    /// <summary>
    /// 已输入字符数。
    /// </summary>
    [ProtoMember(5)]
    public int Progress { get; set; }

    /// <summary>
    /// 试题总字符数。
    /// </summary>
    [ProtoMember(6)]
    public int TotalChars { get; set; }

    /// <summary>
    /// 累计异常次数。
    /// </summary>
    [ProtoMember(7)]
    public int AnomalyCount { get; set; }

    /// <summary>
    /// 时间戳（UTC，由教师端时间同步决定，避免本地改时间作弊）。
    /// </summary>
    [ProtoMember(8)]
    public DateTime Timestamp { get; set; }
}
