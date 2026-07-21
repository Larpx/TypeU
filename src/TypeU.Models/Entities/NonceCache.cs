using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Entities;

/// <summary>
/// Nonce 缓存实体（对应 NonceCache 表，用于防重放）。
/// </summary>
[ProtoContract]
public sealed partial class NonceCache
{
    /// <summary>
    /// Nonce 值（主键）。
    /// </summary>
    [ProtoMember(1)]
    public string Nonce { get; set; } = string.Empty;

    /// <summary>
    /// 接收时间（UTC，用于定期清理）。
    /// </summary>
    [ProtoMember(2)]
    public DateTime ReceivedAt { get; set; }
}
