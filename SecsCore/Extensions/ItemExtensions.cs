using SecsCore.Items;

namespace SecsCore.Extensions;

public static class ItemExtensions
{
    public static T? GetValue<T>(this SecsItem root, params int[] path)
    {
        SecsItem current = root;

        // 1. 钻取 (Drill Down)
        foreach (var index in path)
        {
            if (current is ItemL list && index >= 0 && index < list.Items.Length)
                current = list.Items[index];
            else
                return default;
        }

        // 2. 智能提取 (Smart Extraction)
        return ExtractValue<T>(current);
    }

    private static T? ExtractValue<T>(SecsItem item)
    {
        // 如果是要字符串，直接返回
        if (item is ItemA a && typeof(T) == typeof(string))
            return (T)(object)a.Value;

        // 如果是 Binary，想要 byte[]
        if (item is ItemBinary bin && typeof(T) == typeof(byte[]))
            return (T)(object)bin.Value;

        // === 核心逻辑：处理数值类型 (单值 vs 数组) ===

        // 示例：处理 U4 (uint)
        if (item is ItemU4 u4)
        {
            if (typeof(T) == typeof(uint)) return u4.Value.Length > 0 ? (T)(object)u4.Value[0] : default;
            if (typeof(T) == typeof(uint[])) return (T)(object)u4.Value;
            if (typeof(T) == typeof(int)) return u4.Value.Length > 0 ? (T)(object)(int)u4.Value[0] : default; // 强转
        }

        // 处理 I4 (int)
        if (item is ItemI4 i4)
        {
            if (typeof(T) == typeof(int)) return i4.Value.Length > 0 ? (T)(object)i4.Value[0] : default;
            if (typeof(T) == typeof(int[])) return (T)(object)i4.Value;
        }

        // 处理 Boolean
        if (item is ItemBoolean b && typeof(T) == typeof(bool))
            return (T)(object)b.Value;

        // 处理 F4 (float)
        if (item is ItemF4 f4)
        {
            if (typeof(T) == typeof(float)) return f4.Value.Length > 0 ? (T)(object)f4.Value[0] : default;
        }

        // --- 为了简洁，这里省略了 I1, I2, U1, U2, I8, F8 的重复逻辑 ---
        // --- 在实际产品级代码中，建议使用 Source Generator 或 T4 模板生成这些重复的 switch case ---
        // --- 但作为核心原理展示，上面的逻辑已经覆盖了关键路径 ---

        // 通用兜底：如果T本身就是 SecsItem (比如想取出一个子节点对象)
        if (item is T itemObj) return itemObj;

        return default;
    }
}