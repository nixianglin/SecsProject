namespace SecsCore.Items;

public sealed class ItemBoolean : SecsItem
{
    public override SecsFormat Format => SecsFormat.Boolean;
    public readonly bool Value;

    public ItemBoolean(bool value) => Value = value;

    public override string ToString() => $"<Boolean[{Value}]>"; // 修正：Bool是单值，没有Length
}