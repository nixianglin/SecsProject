namespace SecsCore.Items;

// I1
public sealed class ItemI1 : SecsItem
{
    public override SecsFormat Format => SecsFormat.I1;
    public readonly sbyte[] Value;
    public ItemI1(params sbyte[] value) => Value = value ?? Array.Empty<sbyte>();

    public override string ToString() => $"<I1[{Value.Length}] {string.Join(" ", Value)}>";
}

// I2
public sealed class ItemI2 : SecsItem
{
    public override SecsFormat Format => SecsFormat.I2;
    public readonly short[] Value;
    public ItemI2(params short[] value) => Value = value ?? Array.Empty<short>();

    public override string ToString() => $"<I2[{Value.Length}] {string.Join(" ", Value)}>";
}

// I4
public sealed class ItemI4 : SecsItem
{
    public override SecsFormat Format => SecsFormat.I4;
    public readonly int[] Value;
    public ItemI4(params int[] value) => Value = value ?? Array.Empty<int>();

    public override string ToString() => $"<I4[{Value.Length}] {string.Join(" ", Value)}>";
}

// I8
public sealed class ItemI8 : SecsItem
{
    public override SecsFormat Format => SecsFormat.I8;
    public readonly long[] Value;
    public ItemI8(params long[] value) => Value = value ?? Array.Empty<long>();

    public override string ToString() => $"<I8[{Value.Length}] {string.Join(" ", Value)}>";
}