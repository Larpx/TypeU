using System;
using Larpx.PersonalTools.TypeU.Models.Enums;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Entities;

/// <summary>
/// 学生实体（对应 Students 表）。
/// </summary>
[ProtoContract]
public sealed partial class Student
{
    /// <summary>
    /// 学号（主键）。
    /// </summary>
    [ProtoMember(1)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// 姓名。
    /// </summary>
    [ProtoMember(2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 绑定的设备指纹（CPU ID + MAC + 硬盘序列号哈希）。
    /// </summary>
    [ProtoMember(3)]
    public string DeviceFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// 绑定时间（UTC）。
    /// </summary>
    [ProtoMember(4)]
    public DateTime BoundAt { get; set; }

    /// <summary>
    /// 绑定过期时间（UTC，2 小时后失效）。
    /// </summary>
    [ProtoMember(5)]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 当前状态。
    /// </summary>
    [ProtoMember(6)]
    public StudentStatus Status { get; set; }
}
