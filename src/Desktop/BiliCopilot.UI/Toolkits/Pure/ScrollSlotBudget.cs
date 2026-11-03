// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 卡片网格的尺寸参数.
/// </summary>
/// <param name="ItemWidth">单个卡片的宽度，对应网格布局的最小宽度.</param>
/// <param name="ItemHeight">单个卡片的高度，对应网格布局的最小高度.</param>
/// <param name="ColumnSpacing">列间距.</param>
/// <param name="RowSpacing">行间距.</param>
public readonly record struct SlotLayout(double ItemWidth, double ItemHeight, double ColumnSpacing, double RowSpacing)
{
    /// <summary>
    /// 视频卡片网格的默认尺寸，与 App 资源里的 VideoLayout 保持一致.
    /// </summary>
    public static SlotLayout Video { get; } = new(232, 238, 20, 20);
}

/// <summary>
/// 视口槽位预算：当前视口加上前瞻能放下多少张卡片.
/// </summary>
/// <remarks>
/// 推荐流没有“总数”，能提前算出来的只有“一屏需要多少张”。
/// 这里按布局常量推导而不是读 ScrollView.ExtentHeight，因为 extent 依赖已经渲染的内容，
/// 没有内容时它等于视口高度，无法回答“还缺多少”。
/// </remarks>
public static class ScrollSlotBudget
{
    /// <summary>
    /// 默认前瞻行数：视口之外再多备一行，避免用户刚滚动就露出空白.
    /// </summary>
    public const int DefaultLookaheadRows = 1;

    /// <summary>
    /// 默认槽位上限，避免 4K 全屏时一次预取过多内容.
    /// </summary>
    public const int DefaultMaxSlots = 60;

    /// <summary>
    /// 最小槽位数.
    /// </summary>
    public const int MinSlots = 1;

    /// <summary>
    /// 计算视口需要的槽位数.
    /// </summary>
    /// <param name="viewportWidth">视口宽度，单位为 DIP.</param>
    /// <param name="viewportHeight">视口高度，单位为 DIP.</param>
    /// <param name="layout">卡片网格尺寸.</param>
    /// <param name="lookaheadRows">视口之外额外预留的行数.</param>
    /// <param name="maxSlots">槽位上限.</param>
    /// <returns>槽位数，至少为 <see cref="MinSlots"/>，至多为 <paramref name="maxSlots"/>.</returns>
    public static int Compute(
        double viewportWidth,
        double viewportHeight,
        SlotLayout layout,
        int lookaheadRows = DefaultLookaheadRows,
        int maxSlots = DefaultMaxSlots)
    {
        // 布局尚未完成时尺寸为 0 或 NaN，此时退回一屏最小量，等下一次布局再修正。
        if (!IsUsable(viewportWidth) || !IsUsable(viewportHeight)
            || !IsUsable(layout.ItemWidth) || !IsUsable(layout.ItemHeight))
        {
            return Math.Clamp(1 + Math.Max(0, lookaheadRows), MinSlots, Math.Max(MinSlots, maxSlots));
        }

        var columns = Columns(viewportWidth, layout);
        var rowPitch = layout.ItemHeight + Math.Max(0, layout.RowSpacing);
        var rows = Math.Max(1, (int)Math.Ceiling(viewportHeight / rowPitch)) + Math.Max(0, lookaheadRows);
        var slots = (long)columns * rows;
        return (int)Math.Clamp(slots, MinSlots, Math.Max(MinSlots, maxSlots));
    }

    /// <summary>
    /// 计算一行的列数.
    /// </summary>
    /// <param name="viewportWidth">视口宽度，单位为 DIP.</param>
    /// <param name="layout">卡片网格尺寸.</param>
    /// <returns>列数，至少为 1.</returns>
    public static int Columns(double viewportWidth, SlotLayout layout)
    {
        if (!IsUsable(viewportWidth) || !IsUsable(layout.ItemWidth))
        {
            return 1;
        }

        var spacing = Math.Max(0, layout.ColumnSpacing);
        var pitch = layout.ItemWidth + spacing;
        return pitch <= 0
            ? 1
            : Math.Max(1, (int)Math.Floor((viewportWidth + spacing) / pitch));
    }

    private static bool IsUsable(double value)
        => double.IsFinite(value) && value > 0;
}
