namespace SecsCore.Items;

// 1. Binary (0x20)
public sealed class ItemBinary : SecsItem
{
    public override SecsFormat Format => SecsFormat.Binary;
    public readonly byte[] Value;
    public ItemBinary(params byte[] value) => Value = value ?? Array.Empty<byte>();
    public override string ToString()
    {
        // 转换成 0A 0B FF 这种 Hex 格式，专业！
        var hex = BitConverter.ToString(Value).Replace("-", " ");
        return $"B[{hex}]";
    }
}