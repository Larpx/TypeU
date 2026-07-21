using System;
using System.IO;

namespace Larpx.PersonalTools.TypeU.Network.Protocol;

/// <summary>
/// 报文反序列化器：从字节流解析 Packet。
/// </summary>
public static class PacketReader
{
    /// <summary>
    /// 从缓冲区读取完整报文。
    /// </summary>
    /// <param name="buffer">包含完整报文的字节缓冲区。</param>
    /// <returns>解析后的 Packet。</returns>
    /// <exception cref="InvalidDataException">当缓冲区长度不足以容纳头/尾或字段长度非法时抛出。</exception>
    public static Packet Read(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < PacketConstants.HeaderLength + PacketConstants.TailLength)
        {
            throw new InvalidDataException(
                $"报文长度不足，最小 {PacketConstants.HeaderLength + PacketConstants.TailLength} 字节，实际 {buffer.Length}。");
        }

        using var ms = new MemoryStream(buffer.ToArray(), writable: false);
        using var br = new BinaryReader(ms);

        var packet = new Packet
        {
            Magic = br.ReadUInt16(),
            Version = br.ReadByte(),
            MessageType = (MessageType)br.ReadUInt16(),
            TimestampMs = br.ReadInt64(),
            Nonce = br.ReadBytes(PacketConstants.NonceLength)
        };

        var dataLen = br.ReadInt32();
        if (dataLen < 0)
        {
            throw new InvalidDataException($"DataLen 不能为负数：{dataLen}。");
        }

        var remaining = buffer.Length - PacketConstants.HeaderLength - dataLen - PacketConstants.TailLength;
        if (remaining < 0)
        {
            throw new InvalidDataException(
                $"报文 DataLen={dataLen} 超出缓冲区剩余长度，报文可能被截断。");
        }

        packet.EncryptedData = br.ReadBytes(dataLen);
        packet.Signature = br.ReadBytes(PacketConstants.SignatureLength);
        packet.Tail = br.ReadUInt16();

        return packet;
    }

    /// <summary>
    /// 仅尝试读取固定头并返回 DataLen（用于分帧场景下预判总长度）。
    /// </summary>
    /// <param name="headerBuffer">至少包含 HeaderLength 字节的缓冲区。</param>
    /// <returns>报文总长度（HeaderLength + DataLen + TailLength）。</returns>
    public static int PeekTotalLength(ReadOnlySpan<byte> headerBuffer)
    {
        if (headerBuffer.Length < PacketConstants.HeaderLength)
        {
            throw new InvalidDataException(
                $"头缓冲区长度不足 {PacketConstants.HeaderLength} 字节。");
        }

        var dataLen = BitConverter.ToInt32(headerBuffer.Slice(PacketConstants.HeaderLength - 4, 4));
        if (dataLen < 0)
        {
            throw new InvalidDataException($"DataLen 不能为负数：{dataLen}。");
        }

        return PacketConstants.HeaderLength + dataLen + PacketConstants.TailLength;
    }
}
