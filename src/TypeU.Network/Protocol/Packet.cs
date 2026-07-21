using System;

namespace Larpx.PersonalTools.TypeU.Network.Protocol;

/// <summary>
/// 协议报文结构（已解析）。
/// 对应二进制布局：
/// | Magic(2B) | Version(1B) | MsgType(2B) | Timestamp(8B,ms) | Nonce(16B) | DataLen(4B) | Data(NB) | Signature(32B) | Tail(2B) |。
/// </summary>
public sealed class Packet
{
    /// <summary>
    /// 魔数（固定 0xAA55）。
    /// </summary>
    public ushort Magic { get; set; }

    /// <summary>
    /// 协议版本号。
    /// </summary>
    public byte Version { get; set; }

    /// <summary>
    /// 消息类型。
    /// </summary>
    public MessageType MessageType { get; set; }

    /// <summary>
    /// 时间戳（UTC 毫秒，自 1970-01-01 起计）。
    /// </summary>
    public long TimestampMs { get; set; }

    /// <summary>
    /// Nonce（16 字节随机数，防重放）。
    /// </summary>
    public byte[] Nonce { get; set; } = new byte[PacketConstants.NonceLength];

    /// <summary>
    /// 加密后的 Data 字段（含 IV）。
    /// </summary>
    public byte[] EncryptedData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// HMAC-SHA256 签名（32 字节）。
    /// </summary>
    public byte[] Signature { get; set; } = new byte[PacketConstants.SignatureLength];

    /// <summary>
    /// 尾部标识（固定 0x55AA）。
    /// </summary>
    public ushort Tail { get; set; }

    /// <summary>
    /// 报文总字节长度。
    /// </summary>
    public int TotalLength => PacketConstants.HeaderLength + EncryptedData.Length + PacketConstants.TailLength;
}
