namespace SecsCore.Items;

// F4
public sealed class ItemF4 : SecsItem
{
    public override SecsFormat Format => SecsFormat.F4;
    public readonly float[] Value;
    public ItemF4(params float[] value) => Value = value ?? Array.Empty<float>();

    public override string ToString() => $"<F4[{Value.Length}] {string.Join(" ", Value)}>";
}

// F8
public sealed class ItemF8 : SecsItem
{
    public override SecsFormat Format => SecsFormat.F8;
    public readonly double[] Value;
    public ItemF8(params double[] value) => Value = value ?? Array.Empty<double>();

    public override string ToString() => $"<F8[{Value.Length}] {string.Join(" ", Value)}>";
}