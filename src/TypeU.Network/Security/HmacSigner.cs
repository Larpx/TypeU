using System;
using System.Security.Cryptography;
using Larpx.PersonalTools.TypeU.Network.Protocol;

namespace Larpx.PersonalTools.TypeU.Network.Security;

/// <summary>
/// HMAC-SHA256 签名器，用于报文防篡改。
/// </summary>
public sealed class HmacSigner : IDisposable
{
    private readonly HMACSHA256 _hmac;

    /// <summary>
    /// 初始化 HMAC 签名器。
    /// </summary>
    /// <param name="key">HMAC 密钥（建议 32 字节或以上）。</param>
    public HmacSigner(byte[] key)
    {
        _hmac = new HMACSHA256(key ?? throw new ArgumentNullException(nameof(key)));
    }

    /// <summary>
    /// 计算指定数据的 HMAC-SHA256 签名（32 字节）。
    /// </summary>
    public byte[] Sign(byte[] data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        return _hmac.ComputeHash(data);
    }

    /// <summary>
    /// 验证签名是否匹配（恒定时间比较）。
    /// </summary>
    /// <param name="data">原始数据。</param>
    /// <param name="expectedSignature">期望的签名（32 字节）。</param>
    /// <returns>true 表示签名匹配。</returns>
    public bool Verify(byte[] data, byte[] expectedSignature)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        if (expectedSignature is null || expectedSignature.Length != PacketConstants.SignatureLength)
        {
            return false;
        }

        var actual = _hmac.ComputeHash(data);
        return CryptographicOperations.FixedTimeEquals(actual, expectedSignature);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _hmac.Dispose();
    }
}
