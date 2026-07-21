using System;
using System.Security.Cryptography;

namespace Larpx.PersonalTools.TypeU.Network.Security;

/// <summary>
/// AES-256-CBC 加解密包装器。
/// 密钥固定 32 字节，IV 由调用方提供（建议每次随机 16 字节）。
/// 输出格式：[IV(16B) | Ciphertext(NB)]，对齐 Packet 的 Data 字段。
/// </summary>
public sealed class AesCipher : IDisposable
{
    private const int KeySizeBits = 256;
    private const int IvSizeBytes = 16;
    private readonly Aes _aes;

    /// <summary>
    /// 初始化 AES-256-CBC。
    /// </summary>
    /// <param name="key">32 字节密钥。</param>
    /// <exception cref="ArgumentException">当 key 长度不为 32 时抛出。</exception>
    public AesCipher(byte[] key)
    {
        if (key is null || key.Length != KeySizeBits / 8)
        {
            throw new ArgumentException($"AES-256 需要 32 字节密钥，实际 {key?.Length ?? 0}。", nameof(key));
        }

        _aes = Aes.Create();
        _aes.KeySize = KeySizeBits;
        _aes.Mode = CipherMode.CBC;
        _aes.Padding = PaddingMode.PKCS7;
        _aes.Key = key;
    }

    /// <summary>
    /// 加密明文，输出 [IV | Ciphertext]。
    /// </summary>
    /// <param name="plaintext">明文字节。</param>
    /// <returns>IV(16B) + 密文。</returns>
    public byte[] Encrypt(byte[] plaintext)
    {
        if (plaintext is null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }

        var iv = RandomNumberGenerator.GetBytes(IvSizeBytes);
        using var encryptor = _aes.CreateEncryptor(_aes.Key, iv);
        var cipher = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        var output = new byte[IvSizeBytes + cipher.Length];
        Buffer.BlockCopy(iv, 0, output, 0, IvSizeBytes);
        Buffer.BlockCopy(cipher, 0, output, IvSizeBytes, cipher.Length);
        return output;
    }

    /// <summary>
    /// 解密 [IV | Ciphertext] 数据。
    /// </summary>
    /// <param name="ciphertextWithIv">[IV(16B) | Ciphertext(NB)]。</param>
    /// <returns>明文字节。</returns>
    /// <exception cref="ArgumentException">当输入长度小于 IV 长度时抛出。</exception>
    public byte[] Decrypt(byte[] ciphertextWithIv)
    {
        if (ciphertextWithIv is null)
        {
            throw new ArgumentNullException(nameof(ciphertextWithIv));
        }
        if (ciphertextWithIv.Length < IvSizeBytes)
        {
            throw new ArgumentException(
                $"输入长度不足 IV 长度（{IvSizeBytes} 字节），实际 {ciphertextWithIv.Length}。",
                nameof(ciphertextWithIv));
        }

        var iv = new byte[IvSizeBytes];
        Buffer.BlockCopy(ciphertextWithIv, 0, iv, 0, IvSizeBytes);

        var cipher = new byte[ciphertextWithIv.Length - IvSizeBytes];
        Buffer.BlockCopy(ciphertextWithIv, IvSizeBytes, cipher, 0, cipher.Length);

        using var decryptor = _aes.CreateDecryptor(_aes.Key, iv);
        return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _aes.Dispose();
    }
}
