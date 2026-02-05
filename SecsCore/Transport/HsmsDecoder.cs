using SecsCore.Codec;
using System.Buffers;
using System.Buffers.Binary;

namespace SecsCore.Transport;

public static class HsmsDecoder
{
    // HSMS 长度头是 4 字节
    private const int LengthBytes = 4;

    public static bool TryDecode(ref ReadOnlySequence<byte> input, out SecsMessage? message)
    {
        message = null;

        // 1. 检查数据是否足读取长度头
        if (input.Length < LengthBytes) return false;

        // 2. 读取 4 字节长度 (Big Endian)
        Span<byte> lengthSpan = stackalloc byte[LengthBytes];
        input.Slice(0, LengthBytes).CopyTo(lengthSpan);
        uint messageLength = BinaryPrimitives.ReadUInt32BigEndian(lengthSpan);

        // 3. 检查剩余数据是否足够完整的一个包
        if (input.Length < LengthBytes + messageLength) return false;

        // --- 开始解析完整包 ---

        // 切割出当前包体 (Header + Body)
        var packetSlice = input.Slice(LengthBytes, messageLength);

        // 解析 10 字节 Header
        if (messageLength < 10) throw new Exception("Invalid HSMS Packet");

        Span<byte> headerSpan = stackalloc byte[10];
        packetSlice.Slice(0, 10).CopyTo(headerSpan);
        var header = SecsHeader.Decode(headerSpan);

        // 解析 Body (如果有)
        // 注意：这里为了专家级实现，我们暂时不在这里做 Deep Parse
        // 实际上应该根据 Header.SType == DataMessage 才去解析 Body
        SecsItem? body = null;
        // 只有 DataMessage (SxFy) 才有 Body
        if (messageLength > 10 && header.SType == HsmsType.DataMessage)
        {
            // 【修复 CS1061】
            // ReadOnlySequence 可能是非连续内存，我们需要将其转换为连续内存才能解析
            // 方案 A (极速): 如果只有一段，直接拿 Span
            // 方案 B (通用): 转成数组 (ToArray)

            var bodySequence = packetSlice.Slice(10);

            // 这里的 ToArray() 会发生一次内存拷贝，但保证了 ItemCodec 能拿到连续内存
            // 在工业级极致优化中，我们会用 MemoryPool 租借内存，但这里 ToArray 最安全
            byte[] bodyBytes = bodySequence.ToArray();

            try
            {
                // 现在传入的是 byte[] (隐式转为 ReadOnlySpan)
                body = ItemCodec.Decode(bodyBytes, out int consumed);
            }
            catch (Exception ex)
            {
                // 生产环境建议记录日志
                Console.WriteLine($"Body Parse Warning: {ex.Message}");
            }
        }

        message = new SecsMessage(header, body);

        // 4. 推进 Pipeline 指针
        input = input.Slice(LengthBytes + messageLength);
        return true;
    }
}