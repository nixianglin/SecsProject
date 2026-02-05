using System.Buffers.Binary;

namespace SecsCore;

public readonly record struct SecsHeader
{
    public ushort SessionId { get; init; }    // Device ID
    public bool StreamOrWaitBit { get; init; } // 对于数据消息是 Stream 的最高位(W-Bit)，对于控制消息保留
    public byte Stream { get; init; }
    public byte Function { get; init; }
    public byte PType { get; init; }
    public HsmsType SType { get; init; }      // 关键：区分数据还是控制信令
    public uint SystemBytes { get; init; }

    /// <summary>
    /// 将 Header 编码到 Span 中 (10 字节)
    /// </summary>
    public void EncodeTo(Span<byte> buffer)
    {
        // Bytes 0-1: Session ID
        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(0, 2), SessionId);

        // Byte 2: Header Byte 2 (Stream)
        // 注意：SECS-II 中 W-Bit 位于 Header Byte 3 (Function) 的最高位
        buffer[2] = Stream;

        // Byte 3: Header Byte 3 (Function + W-Bit)
        // 如果是数据消息(SType=0)且需要回复，或者某些控制消息，设置 W-Bit
        byte functionByte = Function;
        if (StreamOrWaitBit) functionByte |= 0x80;
        buffer[3] = functionByte;

        // Byte 4: PType (0)
        buffer[4] = PType;

        // Byte 5: SType (Session Type)
        buffer[5] = (byte)SType;

        // Bytes 6-9: System Bytes
        BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(6, 4), SystemBytes);
    }

    /// <summary>
    /// 从 Span 解码 Header
    /// </summary>
    public static SecsHeader Decode(ReadOnlySpan<byte> buffer)
    {
        return new SecsHeader
        {
            SessionId = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(0, 2)),

            // 【专家修复】: 强行抹去 Stream 的最高位 (R-Bit)，解决 S129 变成 S1 的问题
            Stream = (byte)(buffer[2] & 0x7F),

            // Function 的 W-Bit 我们已经剥离出来了
            Function = (byte)(buffer[3] & 0x7F),

            // 提取 W-Bit 存入 bool
            StreamOrWaitBit = (buffer[3] & 0x80) != 0,

            PType = buffer[4],
            SType = (HsmsType)buffer[5],
            SystemBytes = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(6, 4))
        };
    }
}