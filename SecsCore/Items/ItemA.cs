namespace SecsCore.Items;

public sealed class ItemA : SecsItem
{
    public override SecsFormat Format => SecsFormat.ASCII;
    public readonly string Value;

    public ItemA(string value)
    {
        Value = value ?? string.Empty;
    }

    public override string ToString() => $"<A[{Value.Length}] \"{Value}\">";
}