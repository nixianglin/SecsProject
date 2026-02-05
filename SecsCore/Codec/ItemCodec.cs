using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SecsCore.Items;

namespace SecsCore.Codec;

public static class ItemCodec
{
    /// <summary>
    /// 将 SecsItem 编码写入 IBufferWriter (高性能写入)
    /// </summary>
    public static void Encode(this SecsItem item, IBufferWriter<byte> writer)
    {
        // 1. 计算 Item 的长度 (Value Length)
        // 注意：List 的长度是 Item 个数，其他类型是字节数
        int length = item is ItemL list ? list.Items.Length : GetByteLength(item);

        // 2. 确定长度本身需要几个字节 (1, 2, or 3 bytes)
        byte lengthBytesCount = length switch
        {
            <= 0xFF => 1,
            <= 0xFFFF => 2,
            _ => 3
        };

        // 3. 写入第一个字节: Format Code | Length Bytes Count
        // 关键：这里不需要位移，因为 SecsFormat 枚举值已经是左移过的高6位
        byte headerByte = (byte)((byte)item.Format | lengthBytesCount);

        var span = writer.GetSpan(1 + lengthBytesCount);
        span[0] = headerByte;

        // 4. 写入长度值 (Big Endian)
        if (lengthBytesCount == 1)
        {
            span[1] = (byte)length;
            writer.Advance(2);
        }
        else if (lengthBytesCount == 2)
        {
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(1), (ushort)length);
            writer.Advance(3);
        }
        else // 3 bytes
        {
            span[1] = (byte)(length >> 16);
            span[2] = (byte)(length >> 8);
            span[3] = (byte)length;
            writer.Advance(4);
        }

        // 5. 写入 Value (递归处理 List)
        if (item is ItemL listObj)
        {
            foreach (var child in listObj.Items)
            {
                Encode(child, writer); // 递归
            }
        }
        else
        {
            WriteValue(item, writer);
        }
    }

    private static int GetByteLength(SecsItem item)
    {
        return item switch
        {
            ItemL l => l.Items.Length, // List 实际上不用这个计算字节数，但为了完整性
            ItemA a => a.Value.Length,
            ItemBinary b => b.Value.Length,
            ItemBoolean => 1,

            ItemU1 u1 => u1.Value.Length, // 1 byte
            ItemI1 i1 => i1.Value.Length,

            ItemU2 u2 => u2.Value.Length * 2,
            ItemI2 i2 => i2.Value.Length * 2,

            ItemU4 u4 => u4.Value.Length * 4,
            ItemI4 i4 => i4.Value.Length * 4,
            ItemF4 f4 => f4.Value.Length * 4,

            ItemU8 u8 => u8.Value.Length * 8,
            ItemI8 i8 => i8.Value.Length * 8,
            ItemF8 f8 => f8.Value.Length * 8,

            _ => 0
        };
    }

    private static void WriteValue(SecsItem item, IBufferWriter<byte> writer)
    {
        // === 1. String & Binary ===
        if (item is ItemA itemA)
        {
            if (itemA.Value.Length > 0)
            {
                var bytes = Encoding.ASCII.GetBytes(itemA.Value);
                writer.Write(bytes);
            }
            return;
        }

        if (item is ItemBinary itemBinary)
        {
            if (itemBinary.Value.Length > 0)
            {
                writer.Write(itemBinary.Value);
            }
            return;
        }

        // === 2. Boolean ===
        if (item is ItemBoolean itemBool)
        {
            var span = writer.GetSpan(1);
            span[0] = (byte)(itemBool.Value ? 1 : 0);
            writer.Advance(1);
            return;
        }

        // === 3. 1 Byte Integers (U1, I1) ===
        if (item is ItemU1 itemU1)
        {
            if (itemU1.Value.Length > 0) writer.Write(itemU1.Value);
            return;
        }
        if (item is ItemI1 itemI1)
        {
            if (itemI1.Value.Length > 0)
            {
                var span = writer.GetSpan(itemI1.Value.Length);
                for (int i = 0; i < itemI1.Value.Length; i++)
                    span[i] = (byte)itemI1.Value[i]; // sbyte 转 byte
                writer.Advance(itemI1.Value.Length);
            }
            return;
        }

        // === 4. 2 Bytes Integers (U2, I2) ===
        if (item is ItemU2 itemU2)
        {
            var span = writer.GetSpan(itemU2.Value.Length * 2);
            for (int i = 0; i < itemU2.Value.Length; i++)
                BinaryPrimitives.WriteUInt16BigEndian(span.Slice(i * 2), itemU2.Value[i]);
            writer.Advance(itemU2.Value.Length * 2);
            return;
        }
        if (item is ItemI2 itemI2)
        {
            var span = writer.GetSpan(itemI2.Value.Length * 2);
            for (int i = 0; i < itemI2.Value.Length; i++)
                BinaryPrimitives.WriteInt16BigEndian(span.Slice(i * 2), itemI2.Value[i]);
            writer.Advance(itemI2.Value.Length * 2);
            return;
        }

        // === 5. 4 Bytes Integers & Float (U4, I4, F4) ===
        if (item is ItemU4 itemU4)
        {
            var span = writer.GetSpan(itemU4.Value.Length * 4);
            for (int i = 0; i < itemU4.Value.Length; i++)
                BinaryPrimitives.WriteUInt32BigEndian(span.Slice(i * 4), itemU4.Value[i]);
            writer.Advance(itemU4.Value.Length * 4);
            return;
        }
        if (item is ItemI4 itemI4)
        {
            var span = writer.GetSpan(itemI4.Value.Length * 4);
            for (int i = 0; i < itemI4.Value.Length; i++)
                BinaryPrimitives.WriteInt32BigEndian(span.Slice(i * 4), itemI4.Value[i]);
            writer.Advance(itemI4.Value.Length * 4);
            return;
        }
        if (item is ItemF4 itemF4)
        {
            var span = writer.GetSpan(itemF4.Value.Length * 4);
            for (int i = 0; i < itemF4.Value.Length; i++)
            {
                // Float 转 IntBits，再转 BigEndian
                int intBits = BitConverter.SingleToInt32Bits(itemF4.Value[i]);
                BinaryPrimitives.WriteInt32BigEndian(span.Slice(i * 4), intBits);
            }
            writer.Advance(itemF4.Value.Length * 4);
            return;
        }

        // === 6. 8 Bytes Integers & Double (U8, I8, F8) ===
        if (item is ItemU8 itemU8)
        {
            var span = writer.GetSpan(itemU8.Value.Length * 8);
            for (int i = 0; i < itemU8.Value.Length; i++)
                BinaryPrimitives.WriteUInt64BigEndian(span.Slice(i * 8), itemU8.Value[i]);
            writer.Advance(itemU8.Value.Length * 8);
            return;
        }
        if (item is ItemI8 itemI8)
        {
            var span = writer.GetSpan(itemI8.Value.Length * 8);
            for (int i = 0; i < itemI8.Value.Length; i++)
                BinaryPrimitives.WriteInt64BigEndian(span.Slice(i * 8), itemI8.Value[i]);
            writer.Advance(itemI8.Value.Length * 8);
            return;
        }
        if (item is ItemF8 itemF8)
        {
            var span = writer.GetSpan(itemF8.Value.Length * 8);
            for (int i = 0; i < itemF8.Value.Length; i++)
            {
                // Double 转 LongBits，再转 BigEndian
                long longBits = BitConverter.DoubleToInt64Bits(itemF8.Value[i]);
                BinaryPrimitives.WriteInt64BigEndian(span.Slice(i * 8), longBits);
            }
            writer.Advance(itemF8.Value.Length * 8);
            return;
        }
    }

    /// <summary>
    /// 从 Span 中解码出 SecsItem，并返回消耗的字节数
    /// </summary>
    public static SecsItem Decode(ReadOnlySpan<byte> input, out int bytesConsumed)
    {
        if (input.Length == 0) throw new Exception("Empty buffer");

        // 1. 解析 Header Byte
        byte header = input[0];
        SecsFormat format = (SecsFormat)(header & 0xFC); // 高6位是 Format
        int lenBytesCount = header & 0x03;               // 低2位是长度字节数

        int ptr = 1;

        // 2. 解析 Length
        int itemLength = 0;
        if (lenBytesCount == 1)
        {
            itemLength = input[ptr];
            ptr++;
        }
        else if (lenBytesCount == 2)
        {
            itemLength = BinaryPrimitives.ReadUInt16BigEndian(input.Slice(ptr, 2));
            ptr += 2;
        }
        else if (lenBytesCount == 3)
        {
            itemLength = (input[ptr] << 16) | (input[ptr + 1] << 8) | input[ptr + 2];
            ptr += 3;
        }

        // 3. 根据 Format 解析 Value
        if (format == SecsFormat.List)
        {
            if (itemLength == 0)
            {
                bytesConsumed = ptr;
                return Sml.L();
            }

            var items = new SecsItem[itemLength];
            for (int i = 0; i < itemLength; i++)
            {
                var child = Decode(input.Slice(ptr), out int childConsumed);
                items[i] = child;
                ptr += childConsumed;
            }

            bytesConsumed = ptr;
            return Sml.L(items);
        }
        else
        {
            // 普通类型，itemLength 代表数据字节数
            // 防御性检查
            if (ptr + itemLength > input.Length)
                throw new Exception($"SECS decode error: Not enough data. Expected {itemLength}, Available {input.Length - ptr}");

            var valueSpan = input.Slice(ptr, itemLength);
            var item = ParseValue(format, valueSpan);

            bytesConsumed = ptr + itemLength;
            return item;
        }
    }

    private static SecsItem ParseValue(SecsFormat format, ReadOnlySpan<byte> data)
    {
        return format switch
        {
            SecsFormat.ASCII => Sml.A(Encoding.ASCII.GetString(data)),
            SecsFormat.Binary => Sml.B(data.ToArray()),
            SecsFormat.Boolean => Sml.Boolean(data.Length > 0 && data[0] != 0),

            // 1 Byte
            SecsFormat.U1 => Sml.U1(data.ToArray()), // U1 直接转数组
            SecsFormat.I1 => ParseI1(data),

            // 2 Bytes
            SecsFormat.U2 => ParseU2(data),
            SecsFormat.I2 => ParseI2(data),

            // 4 Bytes
            SecsFormat.U4 => ParseU4(data),
            SecsFormat.I4 => ParseI4(data),
            SecsFormat.F4 => ParseF4(data),

            // 8 Bytes
            SecsFormat.U8 => ParseU8(data),
            SecsFormat.I8 => ParseI8(data),
            SecsFormat.F8 => ParseF8(data),

            _ => throw new NotSupportedException($"Unknown Format: {format}")
        };
    }

    // --- Helper Parsers ---

    private static SecsItem ParseI1(ReadOnlySpan<byte> data)
    {
        // sbyte[]
        sbyte[] values = new sbyte[data.Length];
        for (int i = 0; i < data.Length; i++) values[i] = (sbyte)data[i];
        return Sml.I1(values);
    }

    private static SecsItem ParseU2(ReadOnlySpan<byte> data)
    {
        int count = data.Length / 2;
        var values = new ushort[count];
        for (int i = 0; i < count; i++)
            values[i] = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i * 2));
        return Sml.U2(values);
    }

    private static SecsItem ParseI2(ReadOnlySpan<byte> data)
    {
        int count = data.Length / 2;
        var values = new short[count];
        for (int i = 0; i < count; i++)
            values[i] = BinaryPrimitives.ReadInt16BigEndian(data.Slice(i * 2));
        return Sml.I2(values);
    }

    private static SecsItem ParseU4(ReadOnlySpan<byte> data)
    {
        int count = data.Length / 4;
        var values = new uint[count];
        for (int i = 0; i < count; i++)
            values[i] = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(i * 4));
        return Sml.U4(values);
    }

    private static SecsItem ParseI4(ReadOnlySpan<byte> data)
    {
        int count = data.Length / 4;
        var values = new int[count];
        for (int i = 0; i < count; i++)
            values[i] = BinaryPrimitives.ReadInt32BigEndian(data.Slice(i * 4));
        return Sml.I4(values);
    }

    private static SecsItem ParseF4(ReadOnlySpan<byte> data)
    {
        int count = data.Length / 4;
        var values = new float[count];
        for (int i = 0; i < count; i++)
        {
            int intBits = BinaryPrimitives.ReadInt32BigEndian(data.Slice(i * 4));
            values[i] = BitConverter.Int32BitsToSingle(intBits);
        }
        return Sml.F4(values);
    }

    private static SecsItem ParseU8(ReadOnlySpan<byte> data)
    {
        int count = data.Length / 8;
        var values = new ulong[count];
        for (int i = 0; i < count; i++)
            values[i] = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(i * 8));
        return Sml.U8(values);
    }

    private static SecsItem ParseI8(ReadOnlySpan<byte> data)
    {
        int count = data.Length / 8;
        var values = new long[count];
        for (int i = 0; i < count; i++)
            values[i] = BinaryPrimitives.ReadInt64BigEndian(data.Slice(i * 8));
        return Sml.I8(values);
    }

    private static SecsItem ParseF8(ReadOnlySpan<byte> data)
    {
        int count = data.Length / 8;
        var values = new double[count];
        for (int i = 0; i < count; i++)
        {
            long longBits = BinaryPrimitives.ReadInt64BigEndian(data.Slice(i * 8));
            values[i] = BitConverter.Int64BitsToDouble(longBits);
        }
        return Sml.F8(values);
    }
}