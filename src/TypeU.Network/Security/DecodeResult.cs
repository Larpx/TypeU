namespace Larpx.PersonalTools.TypeU.Network.Security;

/// <summary>
/// 报文解码结果。
/// </summary>
public enum DecodeResult
{
    /// <summary>
    /// 解码成功。
    /// </summary>
    Ok = 0,

    /// <summary>
    /// 缓冲区过短。
    /// </summary>
    TooShort = 1,

    /// <summary>
    /// 报文格式损坏。
    /// </summary>
    Malformed = 2,

    /// <summary>
    /// Magic / Tail 不匹配。
    /// </summary>
    MagicTailMismatch = 3,

    /// <summary>
    /// 签名校验失败（报文被篡改）。
    /// </summary>
    SignatureMismatch = 4,

    /// <summary>
    /// 时间戳偏差超容差。
    /// </summary>
    TimestampSkew = 5,

    /// <summary>
    /// 检测到重放（Nonce 重复）。
    /// </summary>
    ReplayDetected = 6,

    /// <summary>
    /// 解密失败（AES）。
    /// </summary>
    DecryptFailed = 7
}
