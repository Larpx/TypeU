using System;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Network;

/// <summary>
/// PacketCodec 协议编解码、签名校验、防重放测试。
/// </summary>
public class PacketCodecTests
{
    private static byte[] NewAesKey() => System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    private static byte[] NewHmacKey() => System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

    /// <summary>
    /// 正常往返编解码应成功并还原 payload。
    /// </summary>
    [Fact]
    public void EncodeDecode_RoundTrip_PreservesPayload()
    {
        var aesKey = NewAesKey();
        var hmacKey = NewHmacKey();
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        using var codec = new PacketCodec(aesKey, hmacKey, verifyNonce: true);
        var bytes = codec.Encode(MessageType.Login, payload, TimestampValidator.NowUtcMs());

        var result = codec.Decode(bytes, out var msgType, out var decoded);

        Assert.Equal(DecodeResult.Ok, result);
        Assert.Equal(MessageType.Login, msgType);
        Assert.Equal(payload, decoded);
    }

    /// <summary>
    /// 篡改 Data 字段后签名校验应失败。
    /// </summary>
    [Fact]
    public void Decode_TamperedData_ReturnsSignatureMismatch()
    {
        var aesKey = NewAesKey();
        var hmacKey = NewHmacKey();
        var payload = new byte[] { 11, 22, 33 };

        using var codec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);
        var bytes = codec.Encode(MessageType.Login, payload, TimestampValidator.NowUtcMs());

        // 翻转 Data 字段中的某个字节（HeaderLength 之后第一个 Data 字节）。
        var tampered = (byte[])bytes.Clone();
        tampered[PacketConstants.HeaderLength] ^= 0xFF;

        var result = codec.Decode(tampered, out _, out _);

        Assert.Equal(DecodeResult.SignatureMismatch, result);
    }

    /// <summary>
    /// 篡改 Magic 应返回 MagicTailMismatch。
    /// </summary>
    [Fact]
    public void Decode_WrongMagic_ReturnsMagicTailMismatch()
    {
        var aesKey = NewAesKey();
        var hmacKey = NewHmacKey();

        using var codec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);
        var bytes = codec.Encode(MessageType.Login, new byte[] { 1 }, TimestampValidator.NowUtcMs());

        var tampered = (byte[])bytes.Clone();
        tampered[0] = 0x00; // 破坏 Magic 第一字节。

        var result = codec.Decode(tampered, out _, out _);

        Assert.Equal(DecodeResult.MagicTailMismatch, result);
    }

    /// <summary>
    /// 过期时间戳应被拒绝。
    /// </summary>
    [Fact]
    public void Decode_ExpiredTimestamp_ReturnsTimestampSkew()
    {
        var aesKey = NewAesKey();
        var hmacKey = NewHmacKey();

        using var codec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);
        // 取 5 分钟前的时间戳，超过 60 秒容差。
        var oldMs = TimestampValidator.NowUtcMs() - 5 * 60 * 1000;
        var bytes = codec.Encode(MessageType.Login, new byte[] { 1, 2 }, oldMs);

        var result = codec.Decode(bytes, out _, out _);

        Assert.Equal(DecodeResult.TimestampSkew, result);
    }

    /// <summary>
    /// 同一 Nonce 的报文再次到达应被拒绝（重放）。
    /// </summary>
    [Fact]
    public void Decode_ReplayedNonce_ReturnsReplayDetected()
    {
        var aesKey = NewAesKey();
        var hmacKey = NewHmacKey();

        using var codec = new PacketCodec(aesKey, hmacKey, verifyNonce: true);
        var bytes = codec.Encode(MessageType.Login, new byte[] { 1 }, TimestampValidator.NowUtcMs());

        var first = codec.Decode(bytes, out _, out _);
        var second = codec.Decode(bytes, out _, out _);

        Assert.Equal(DecodeResult.Ok, first);
        Assert.Equal(DecodeResult.ReplayDetected, second);
    }

    /// <summary>
    /// 关闭防重放校验后，同一 Nonce 报文应能解码（用于不关心重放的场景，如客户端）。
    /// </summary>
    [Fact]
    public void Decode_NoNonceCheck_AcceptsReplay()
    {
        var aesKey = NewAesKey();
        var hmacKey = NewHmacKey();

        using var codec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);
        var bytes = codec.Encode(MessageType.Login, new byte[] { 1 }, TimestampValidator.NowUtcMs());

        var first = codec.Decode(bytes, out _, out _);
        var second = codec.Decode(bytes, out _, out _);

        Assert.Equal(DecodeResult.Ok, first);
        Assert.Equal(DecodeResult.Ok, second);
    }

    /// <summary>
    /// 不同密钥解密应失败。
    /// </summary>
    [Fact]
    public void Decode_DifferentKey_ReturnsSignatureMismatch()
    {
        var aesKey1 = NewAesKey();
        var hmacKey1 = NewHmacKey();
        var aesKey2 = NewAesKey();
        var hmacKey2 = NewHmacKey();

        using var codec1 = new PacketCodec(aesKey1, hmacKey1, verifyNonce: false);
        using var codec2 = new PacketCodec(aesKey2, hmacKey2, verifyNonce: false);

        var bytes = codec1.Encode(MessageType.Login, new byte[] { 1 }, TimestampValidator.NowUtcMs());
        var result = codec2.Decode(bytes, out _, out _);

        Assert.Equal(DecodeResult.SignatureMismatch, result);
    }

    /// <summary>
    /// 短缓冲区应返回 TooShort。
    /// </summary>
    [Fact]
    public void Decode_ShortBuffer_ReturnsTooShort()
    {
        var aesKey = NewAesKey();
        var hmacKey = NewHmacKey();

        using var codec = new PacketCodec(aesKey, hmacKey, verifyNonce: false);
        var tiny = new byte[10];

        var result = codec.Decode(tiny, out _, out _);

        Assert.Equal(DecodeResult.TooShort, result);
    }
}
