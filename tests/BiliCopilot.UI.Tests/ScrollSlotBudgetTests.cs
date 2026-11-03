using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class ScrollSlotBudgetTests
{
    [Fact]
    public void ComputesColumnsAndOneExtraRowForLookahead()
    {
        var slots = ScrollSlotBudget.Compute(1200, 700, SlotLayout.Video);

        // 4 列；700/258 向上取整为 3 行，再加 1 行前瞻。
        Assert.Equal(16, slots);
    }

    [Fact]
    public void NarrowViewportStillGetsOneColumn()
    {
        Assert.Equal(4, ScrollSlotBudget.Compute(200, 600, SlotLayout.Video));
    }

    [Fact]
    public void LookaheadCanBeDisabled()
    {
        Assert.Equal(12, ScrollSlotBudget.Compute(1000, 600, SlotLayout.Video, lookaheadRows: 0));
    }

    [Fact]
    public void CapsLargeViewportAtConfiguredLimit()
    {
        // 4000 宽约 15 列、2000 高约 9 行，远超上限。
        Assert.Equal(ScrollSlotBudget.DefaultMaxSlots, ScrollSlotBudget.Compute(4000, 2000, SlotLayout.Video));
        Assert.Equal(12, ScrollSlotBudget.Compute(4000, 2000, SlotLayout.Video, maxSlots: 12));
    }

    [Fact]
    public void DegenerateLayoutFallsBackToMinimumInsteadOfThrowing()
    {
        Assert.Equal(2, ScrollSlotBudget.Compute(0, 0, SlotLayout.Video));
        Assert.Equal(2, ScrollSlotBudget.Compute(double.NaN, 600, SlotLayout.Video));
        Assert.Equal(1, ScrollSlotBudget.Compute(1000, 600, SlotLayout.Video, maxSlots: 0));
    }

    [Theory]
    [InlineData(484, 2)]
    [InlineData(483, 1)]
    [InlineData(736, 3)]
    public void ColumnCountUsesItemPitchIncludingSpacing(double width, int expected)
    {
        Assert.Equal(expected, ScrollSlotBudget.Columns(width, SlotLayout.Video));
    }
}
