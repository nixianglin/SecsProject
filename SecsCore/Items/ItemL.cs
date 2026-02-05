namespace SecsCore.Items;

public sealed class ItemL : SecsItem
{
    public override SecsFormat Format => SecsFormat.List;

    // 直接用数组，为了读取速度快
    public readonly SecsItem[] Items;

    public ItemL(params SecsItem[] items)
    {
        Items = items ?? [];
    }

    // 调试显示：L[3] 表示里面有3个元素
    public override string ToString()
    {
        if (Items.Length == 0) return "L[0]";

        // 递归调用子项的 ToString()，用逗号连接
        // 效果: L[ U4[1001], L[ "JOB01", ... ] ]
        var childrenStr = string.Join(", ", Items.Select(i => i.ToString()));

        return $"L[ {childrenStr} ]";
    }
}