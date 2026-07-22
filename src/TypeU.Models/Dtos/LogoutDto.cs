using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 学生主动登出请求。
/// </summary>
[ProtoContract]
public sealed partial class LogoutDto
{
    /// <summary>学号。</summary>
    [ProtoMember(1)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>设备指纹。</summary>
    [ProtoMember(2)]
    public string DeviceFingerprint { get; set; } = string.Empty;

    /// <summary>会话 ID。</summary>
    [ProtoMember(3)]
    public Guid SessionId { get; set; }
}
