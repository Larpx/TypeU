using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 学生连接握手（学生端 → 教师端）。
/// </summary>
[ProtoContract]
public sealed partial class HelloDto
{
    /// <summary>设备指纹。</summary>
    [ProtoMember(1)]
    public string DeviceFingerprint { get; set; } = string.Empty;

    /// <summary>计算机名。</summary>
    [ProtoMember(2)]
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>客户端自称 IP（可选）。</summary>
    [ProtoMember(3)]
    public string ClientIp { get; set; } = string.Empty;
}
