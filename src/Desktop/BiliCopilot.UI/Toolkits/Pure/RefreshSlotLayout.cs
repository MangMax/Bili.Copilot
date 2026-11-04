// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 增量刷新时列表的槽位排布.
/// </summary>
/// <remarks>
/// 刷新开始时先按目标条数在顶部预留位置，新卡片到达后占据预留区顶部，
/// 预留区总高度保持不变，因此旧内容不会被一页一页往下顶.
/// 渲染顺序：新卡片 → 剩余占位 → 旧卡片 → 尾巴占位（补齐视口预算）.
/// </remarks>
public readonly record struct RefreshSlotLayout
{
    private RefreshSlotLayout(int inserted, int remaining, int oldCount, int tailCount)
    {
        Inserted = inserted;
        Remaining = remaining;
        OldCount = oldCount;
        TailCount = tailCount;
    }

    /// <summary>
    /// 已经到达、占据预留区顶部的新卡片数量.
    /// </summary>
    public int Inserted { get; }

    /// <summary>
    /// 预留区里还没被填上的占位数量.
    /// </summary>
    public int Remaining { get; }

    /// <summary>
    /// 刷新前就存在的旧卡片数量.
    /// </summary>
    public int OldCount { get; }

    /// <summary>
    /// 列表不足一屏时，末尾用于补齐视口预算的占位数量.
    /// </summary>
    public int TailCount { get; }

    /// <summary>
    /// 渲染序列的总长度（恒定，等于预留高度 + 旧内容 + 尾巴占位）.
    /// </summary>
    public int TotalCount => Inserted + Remaining + OldCount + TailCount;

    /// <summary>
    /// 计算槽位排布.
    /// </summary>
    /// <param name="totalCount">业务列表当前长度（新卡片在前、旧卡片在后）.</param>
    /// <param name="baselineCount">刷新开始时的列表长度.</param>
    /// <param name="pendingCount">本次刷新还要取回的条数，非增量刷新传 0.</param>
    /// <param name="budget">当前视口预算.</param>
    /// <returns>槽位排布.</returns>
    public static RefreshSlotLayout Compute(int totalCount, int baselineCount, int pendingCount, int budget)
    {
        var inserted = pendingCount > 0
            ? Math.Min(pendingCount, Math.Max(0, totalCount - baselineCount))
            : 0;
        var remaining = Math.Max(0, pendingCount - inserted);
        var oldCount = totalCount - inserted;
        var tail = Math.Max(0, budget - totalCount - remaining);
        return new RefreshSlotLayout(inserted, remaining, oldCount, tail);
    }

    /// <summary>
    /// 取某个槽位对应的业务列表下标.
    /// </summary>
    /// <param name="slot">槽位序号.</param>
    /// <returns>业务列表下标；返回 -1 表示该位置应放骨架占位.</returns>
    public int VideoIndexAt(int slot)
    {
        if (slot < 0 || slot >= TotalCount)
        {
            return -1;
        }

        if (slot < Inserted)
        {
            return slot;
        }

        if (slot < Inserted + Remaining)
        {
            return -1;
        }

        var index = slot - Remaining;
        return index < Inserted + OldCount ? index : -1;
    }
}
