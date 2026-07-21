using System;
using System.Security.Cryptography;
using Larpx.PersonalTools.TypeU.Network.Protocol;

namespace Larpx.PersonalTools.TypeU.Network.Security;

/// <summary>
/// 报文编解码器：组合 AES 加解密 + HMAC 签名 + Nonce/Timestamp 校验。
/// 调用方负责生成 Nonce；本类负责填充字段、计算签名与序列化。
/// </summary>
public sealed class PacketCodec : IDisposable
{
    private readonly AesCipher _aes;
    private readonly HmacSigner _hmac;
    private readonly TimestampValidator _timestampValidator;
    private readonly NonceCache? _nonceCache;

    /// <summary>
    /// 初始化报文编解码器。
    /// </summary>
    /// <param name="aesKey">AES-256 密钥（32 字节）。</param>
    /// <param name="hmacKey">HMAC 密钥。</param>
    /// <param name="verifyNonce">是否启用 Nonce 防重放（服务端 true，客户端通常 false）。</param>
    public PacketCodec(byte[] aesKey, byte[] hmacKey, bool verifyNonce)
    {
        _aes = new AesCipher(aesKey);
        _hmac = new HmacSigner(hmacKey);
        _timestampValidator = new TimestampValidator();
        _nonceCache = verifyNonce ? new NonceCache() : null;
    }

    /// <summary>
    /// 编码业务消息为完整报文字节流。
    /// </summary>
    /// <param name="messageType">消息类型。</param>
    /// <param name="payload">明文业务消息（通常是 protobuf 序列化结果）。</param>
    /// <param name="timestampMs">时间戳（UTC 毫秒）。</param>
    /// <returns>完整报文字节流。</returns>
    public byte[] Encode(MessageType messageType, byte[] payload, long timestampMs)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        var nonce = RandomNumberGenerator.GetBytes(PacketConstants.NonceLength);
        var encrypted = _aes.Encrypt(payload);

        var packet = new Packet
        {
            Magic = PacketConstants.Magic,
            Version = PacketConstants.Version,
            MessageType = messageType,
            TimestampMs = timestampMs,
            Nonce = nonce,
            EncryptedData = encrypted,
            Tail = PacketConstants.Tail
        };

        var signedBytes = PacketWriter.ComputeSignedRegion(packet);
        packet.Signature = _hmac.Sign(signedBytes);

        return PacketWriter.Write(packet);
    }

    /// <summary>
    /// 解码完整报文字节流为业务消息（已校验 Magic/Tail/HMAC/Timestamp/Nonce）。
    /// </summary>
    /// <param name="buffer">完整报文字节流。</param>
    /// <param name="messageType">输出消息类型。</param>
    /// <param name="payload">输出明文业务消息。</param>
    /// <returns>解码结果状态。</returns>
    public DecodeResult Decode(ReadOnlySpan<byte> buffer, out MessageType messageType, out byte[] payload)
    {
        messageType = MessageType.Unknown;
        payload = Array.Empty<byte>();

        if (buffer.Length < PacketConstants.HeaderLength + PacketConstants.TailLength)
        {
            return DecodeResult.TooShort;
        }

        Packet packet;
        try
        {
            packet = PacketReader.Read(buffer);
        }
        catch (Exception)
        {
            return DecodeResult.Malformed;
        }

        if (packet.Magic != PacketConstants.Magic || packet.Tail != PacketConstants.Tail)
        {
            return DecodeResult.MagicTailMismatch;
        }

        var signedBytes = PacketWriter.ComputeSignedRegion(packet);
        if (!_hmac.Verify(signedBytes, packet.Signature))
        {
            return DecodeResult.SignatureMismatch;
        }

        if (!_timestampValidator.IsValid(packet.TimestampMs, DateTime.UtcNow))
        {
            return DecodeResult.TimestampSkew;
        }

        if (_nonceCache is not null)
        {
            if (!_nonceCache.TryAdd(packet.Nonce, Environment.TickCount64))
            {
                return DecodeResult.ReplayDetected;
            }
        }

        try
        {
            payload = _aes.Decrypt(packet.EncryptedData);
        }
        catch (CryptographicException)
        {
            return DecodeResult.DecryptFailed;
        }

        messageType = packet.MessageType;
        return DecodeResult.Ok;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _aes.Dispose();
        _hmac.Dispose();
    }
}
