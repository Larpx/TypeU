namespace Larpx.PersonalTools.TypeU.Network.Protocol;

/// <summary>
/// 二进制协议常量。
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// 报文起始魔数（2 字节，0xAA55）。
    /// </summary>
    public const ushort Magic = 0xAA55;

    /// <summary>
    /// 报文尾部标识（2 字节，0x55AA）。
    /// </summary>
    public const ushort Tail = 0x55AA;

    /// <summary>
    /// 协议版本号。
    /// </summary>
    public const byte Version = 1;

    /// <summary>
    /// 报文固定头长度（Magic2 + Version1 + MsgType2 + Timestamp8 + Nonce16 + DataLen4 = 33）。
    /// </summary>
    public const int HeaderLength = 33;

    /// <summary>
    /// 报文尾部固定长度（Signature32 + Tail2 = 34）。
    /// </summary>
    public const int TailLength = 34;

    /// <summary>
    /// HMAC-SHA256 签名长度（32 字节）。
    /// </summary>
    public const int SignatureLength = 32;

    /// <summary>
    /// Nonce 长度（16 字节）。
    /// </summary>
    public const int NonceLength = 16;

    /// <summary>
    /// 时间戳容差（秒）。
    /// </summary>
    public const int TimestampToleranceSeconds = 60;

    /// <summary>
    /// Nonce 缓存窗口（秒）。
    /// </summary>
    public const int NonceCacheWindowSeconds = 120;
}
