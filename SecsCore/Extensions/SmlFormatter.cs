using System.Text;
using SecsCore.Items;

namespace SecsCore.Extensions;

public static class SmlFormatter
{
    private const int IndentSize = 2;

    public static string ToSml(this SecsItem item)
    {
        if (item == null) return " <Null>";
        var sb = new StringBuilder();

        // 为了第一行对齐美观，我们在开始时不加缩进，但在递归内部处理缩进
        FormatRecursive(sb, item, 0);

        // 处理一下结尾的换行
        return sb.ToString().TrimEnd();
    }

    private static void FormatRecursive(StringBuilder sb, SecsItem item, int indentLevel)
    {
        // 生成缩进字符串
        var indent = new string(' ', indentLevel * IndentSize);

        // 获取简写类型 (L, A, B, BOOL, U4...)
        string type = GetShortName(item);
        int len = GetLength(item);

        if (item is ItemL list)
        {
            // --- 列表 ---
            // 格式: <L [n]
            sb.Append($"{indent}<{type} [{len}]");

            if (list.Items.Length > 0)
            {
                sb.AppendLine(); // 换行
                foreach (var child in list.Items)
                {
                    FormatRecursive(sb, child, indentLevel + 1); // 递归
                }
                // 闭合 >
                sb.AppendLine($"{indent}>");
            }
            else
            {
                // 空列表: <L [0] >
                sb.AppendLine(" >");
            }
        }
        else
        {
            // --- 数值 ---
            // 格式: <A [n] "Value">
            string valStr = GetValueString(item);
            sb.AppendLine($"{indent}<{type} [{len}] {valStr}>");
        }
    }

    // 【核心修复】将枚举转换为简写
    private static string GetShortName(SecsItem item)
    {
        return item.Format switch
        {
            SecsFormat.List => "L",
            SecsFormat.ASCII => "A",
            SecsFormat.Binary => "B",
            SecsFormat.Boolean => "BOOL",
            _ => item.Format.ToString()
        };
    }

    private static int GetLength(SecsItem item)
    {
        return item switch
        {
            ItemL l => l.Items.Length,
            ItemA a => a.Value.Length,
            ItemBinary b => b.Value.Length,
            ItemBoolean => 1,
            ItemU1 u1 => u1.Value.Length,
            ItemU2 u2 => u2.Value.Length,
            ItemU4 u4 => u4.Value.Length,
            ItemU8 u8 => u8.Value.Length,
            ItemI1 i1 => i1.Value.Length,
            ItemI2 i2 => i2.Value.Length,
            ItemI4 i4 => i4.Value.Length,
            ItemI8 i8 => i8.Value.Length,
            ItemF4 f4 => f4.Value.Length,
            ItemF8 f8 => f8.Value.Length,
            _ => 0
        };
    }

    private static string GetValueString(SecsItem item)
    {
        return item switch
        {
            ItemA a => $"\"{a.Value}\"",
            ItemBinary b => FormatHex(b.Value),
            ItemBoolean b => b.Value.ToString(), // True/False

            // 数组类型用空格分隔
            ItemU1 u1 => string.Join(" ", u1.Value),
            ItemU2 u2 => string.Join(" ", u2.Value),
            ItemU4 u4 => string.Join(" ", u4.Value),
            ItemU8 u8 => string.Join(" ", u8.Value),
            ItemI1 i1 => string.Join(" ", i1.Value),
            ItemI2 i2 => string.Join(" ", i2.Value),
            ItemI4 i4 => string.Join(" ", i4.Value),
            ItemI8 i8 => string.Join(" ", i8.Value),
            ItemF4 f4 => string.Join(" ", f4.Value),
            ItemF8 f8 => string.Join(" ", f8.Value),
            _ => ""
        };
    }

    private static string FormatHex(byte[] data)
    {
        if (data.Length == 0) return "";
        return "0x" + BitConverter.ToString(data).Replace("-", " 0x");
    }
}