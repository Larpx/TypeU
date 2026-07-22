using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Entities;

/// <summary>
/// 考试会话内设备登录记录（对应 SessionLogins 表）。
/// </summary>
[ProtoContract]
public sealed partial class SessionLogin
{
    /// <summary>考试会话 ID。</summary>
    [ProtoMember(1)]
    public Guid SessionId { get; set; }

    /// <summary>设备指纹。</summary>
    [ProtoMember(2)]
    public string DeviceFingerprint { get; set; } = string.Empty;

    /// <summary>学号。</summary>
    [ProtoMember(3)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    [ProtoMember(4)]
    public string Name { get; set; } = string.Empty;

    /// <summary>登录时间（UTC）。</summary>
    [ProtoMember(5)]
    public DateTime LoggedInAt { get; set; }

    /// <summary>教师是否已允许该生登出。</summary>
    [ProtoMember(6)]
    public bool LogoutAllowed { get; set; }
}
