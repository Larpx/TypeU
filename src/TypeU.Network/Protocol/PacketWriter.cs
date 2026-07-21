using System;
using System.IO;

namespace Larpx.PersonalTools.TypeU.Network.Protocol;

/// <summary>
/// 报文序列化器：将 Packet 写入字节流。
/// </summary>
public static class PacketWriter
{
    /// <summary>
    /// 将 Packet 序列化为字节数组（不含 Signature，由调用方在写入前填充）。
    /// </summary>
    /// <param name="packet">待序列化的报文。</param>
    /// <returns>完整报文字节流。</returns>
    /// <exception cref="InvalidOperationException">当 Nonce 长度非法时抛出。</exception>
    public static byte[] Write(Packet packet)
    {
        if (packet.Nonce.Length != PacketConstants.NonceLength)
        {
            throw new InvalidOperationException(
                $"Nonce 长度必须为 {PacketConstants.NonceLength} 字节，实际 {packet.Nonce.Length}。");
        }
        if (packet.Signature.Length != PacketConstants.SignatureLength)
        {
            throw new InvalidOperationException(
                $"Signature 长度必须为 {PacketConstants.SignatureLength} 字节，实际 {packet.Signature.Length}。");
        }

        var buffer = new byte[packet.TotalLength];
        using var ms = new MemoryStream(buffer, writable: true);
        using var bw = new BinaryWriter(ms);

        bw.Write(packet.Magic);
        bw.Write(packet.Version);
        bw.Write((ushort)packet.MessageType);
        bw.Write(packet.TimestampMs);
        bw.Write(packet.Nonce);
        bw.Write(packet.EncryptedData.Length);
        bw.Write(packet.EncryptedData);
        bw.Write(packet.Signature);
        bw.Write(packet.Tail);

        return buffer;
    }

    /// <summary>
    /// 计算 Signature 字段：对 Magic + Version + MsgType + Timestamp + Nonce + DataLen + Data 取 HMAC-SHA256。
    /// </summary>
    /// <param name="packet">报文（Signature 字段将被忽略）。</param>
    /// <returns>32 字节签名。</returns>
    public static byte[] ComputeSignedRegion(Packet packet)
    {
        var signedLength = PacketConstants.HeaderLength + packet.EncryptedData.Length;
        var buffer = new byte[signedLength];
        using var ms = new MemoryStream(buffer, writable: true);
        using var bw = new BinaryWriter(ms);

        bw.Write(packet.Magic);
        bw.Write(packet.Version);
        bw.Write((ushort)packet.MessageType);
        bw.Write(packet.TimestampMs);
        bw.Write(packet.Nonce);
        bw.Write(packet.EncryptedData.Length);
        bw.Write(packet.EncryptedData);

        return buffer;
    }
}
