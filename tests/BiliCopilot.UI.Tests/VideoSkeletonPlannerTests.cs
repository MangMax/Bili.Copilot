using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class VideoSkeletonPlannerTests
{
    [Fact]
    public void SkeletonLayerAlwaysCoversTheWholeBudget()
    {
        // 已到 6 张时仍然铺满 16 格：多出来的部分被真实卡片盖住，
        // 这样才能填满最后一行留下的空位。
        var plan = VideoSkeletonPlanner.Resolve(16, 6);

        Assert.Equal(16, plan.SlotCount);
        Assert.True(plan.Visible);
    }

    [Fact]
    public void LayerHidesOnceRealCardsReachTheBudget()
    {
        Assert.False(VideoSkeletonPlanner.Resolve(16, 16).Visible);
        Assert.False(VideoSkeletonPlanner.Resolve(16, 20).Visible);
    }

    [Fact]
    public void EmptyBudgetProducesNoLayer()
    {
        var plan = VideoSkeletonPlanner.Resolve(-1, 0);

        Assert.Equal(0, plan.SlotCount);
        Assert.False(plan.Visible);
    }
}
