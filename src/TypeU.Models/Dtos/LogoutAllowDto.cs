using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 教师允许指定学生登出（教师端 → 学生端）。
/// </summary>
[ProtoContract]
public sealed partial class LogoutAllowDto
{
    /// <summary>学号。</summary>
    [ProtoMember(1)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>会话 ID。</summary>
    [ProtoMember(2)]
    public Guid SessionId { get; set; }

    /// <summary>是否允许。</summary>
    [ProtoMember(3)]
    public bool Allowed { get; set; } = true;
}
