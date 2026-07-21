using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 教师端登录响应 DTO（LoginAck，教师端 → 学生端）。
/// </summary>
[ProtoContract]
public sealed partial class LoginAckDto
{
    /// <summary>是否允许登录。</summary>
    [ProtoMember(1)]
    public bool Success { get; set; }

    /// <summary>失败原因（已绑定其他设备等）。</summary>
    [ProtoMember(2)]
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>教师端时间戳（UTC 毫秒）。</summary>
    [ProtoMember(3)]
    public long ServerTimestampMs { get; set; }
}
