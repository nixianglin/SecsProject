namespace SecsCore.Items;

// U1
public sealed class ItemU1 : SecsItem
{
    public override SecsFormat Format => SecsFormat.U1;
    public readonly byte[] Value;
    public ItemU1(params byte[] value) => Value = value ?? Array.Empty<byte>();

    public override string ToString() => $"<U1[{Value.Length}] {string.Join(" ", Value)}>";
}

// U2
public sealed class ItemU2 : SecsItem
{
    public override SecsFormat Format => SecsFormat.U2;
    public readonly ushort[] Value;
    public ItemU2(params ushort[] value) => Value = value ?? Array.Empty<ushort>();

    public override string ToString() => $"<U2[{Value.Length}] {string.Join(" ", Value)}>";
}

// U4
public sealed class ItemU4 : SecsItem
{
    public override SecsFormat Format => SecsFormat.U4;
    public readonly uint[] Value;
    public ItemU4(params uint[] value) => Value = value ?? Array.Empty<uint>();

    public override string ToString() => $"<U4[{Value.Length}] {string.Join(" ", Value)}>";
}

// U8
public sealed class ItemU8 : SecsItem
{
    public override SecsFormat Format => SecsFormat.U8;
    public readonly ulong[] Value;
    public ItemU8(params ulong[] value) => Value = value ?? Array.Empty<ulong>();

    public override string ToString() => $"<U8[{Value.Length}] {string.Join(" ", Value)}>";
}