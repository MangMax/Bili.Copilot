// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 骨架层的铺设方案.
/// </summary>
/// <param name="SlotCount">要铺多少张骨架卡片.</param>
/// <param name="Visible">骨架层是否可见.</param>
public readonly record struct VideoSkeletonPlan(int SlotCount, bool Visible);

/// <summary>
/// 根据槽位预算决定骨架层怎么铺.
/// </summary>
/// <remarks>
/// 骨架铺满整份预算并垫在真实卡片下面，而不是只铺「还差几张」：
/// 卡片网格是按行等分宽度的，最后一行常常只占了一半，
/// 只补差额会让骨架从下一行开始，反而多出一行高度。
/// </remarks>
public static class VideoSkeletonPlanner
{
    /// <summary>
    /// 计算骨架层的铺设方案.
    /// </summary>
    /// <param name="budget">视口槽位预算.</param>
    /// <param name="itemCount">列表里真实卡片的数量.</param>
    /// <returns>骨架数量与是否可见.</returns>
    public static VideoSkeletonPlan Resolve(int budget, int itemCount)
    {
        var slots = Math.Max(0, budget);
        return new VideoSkeletonPlan(slots, itemCount < slots);
    }
}
