using System.Diagnostics;

namespace SecsCore;

/// <summary>
/// 所有 SECS 数据项的基类
/// </summary>
[DebuggerDisplay("{Format}")]
public abstract class SecsItem
{
    public abstract SecsFormat Format { get; }

    // 方便调试看日志
    public override string ToString() => $"<{Format}>";
}