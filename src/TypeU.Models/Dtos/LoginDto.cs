using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 学生签到登录请求 DTO。
/// </summary>
[ProtoContract]
public sealed partial class LoginDto
{
    /// <summary>
    /// 学号。
    /// </summary>
    [ProtoMember(1)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// 姓名。
    /// </summary>
    [ProtoMember(2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 设备指纹。
    /// </summary>
    [ProtoMember(3)]
    public string DeviceFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// 计算机名（可选，用于教师端展示）。
    /// </summary>
    [ProtoMember(4)]
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// 客户端 IP（可选，由教师端填充）。
    /// </summary>
    [ProtoMember(5)]
    public string ClientIp { get; set; } = string.Empty;
}
