// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 推荐子分区刷新结果能否写回当前列表的判定。
/// </summary>
/// <remarks>
/// 刷新请求在路上时，用户可能切到别的子分区、切换排序或再次刷新，
/// 这些动作都会让列表代次递增；请求返回时代次已变，说明这批结果不属于当前列表。
/// 只看「现在是不是推荐子分区」不够：切走再切回推荐子分区后它仍然是推荐子分区，
/// 但列表已经被别的动作改过，旧请求的结果不能覆盖它。
/// </remarks>
public class RecommendRefreshResultGuardTests
{
    [Fact]
    public void ShouldApply_StillRecommendAndSameGeneration_ReturnsTrue()
        => Assert.True(RecommendRefreshResultGuard.ShouldApply(true, 3, 3));

    [Fact]
    public void ShouldApply_GenerationChangedBySwitchingChildPartition_ReturnsFalse()
        => Assert.False(RecommendRefreshResultGuard.ShouldApply(true, 3, 4));

    [Fact]
    public void ShouldApply_GenerationChangedBySwitchingSortType_ReturnsFalse()
        => Assert.False(RecommendRefreshResultGuard.ShouldApply(true, 7, 8));

    [Fact]
    public void ShouldApply_SwitchedAwayFromRecommend_ReturnsFalse()
        => Assert.False(RecommendRefreshResultGuard.ShouldApply(false, 3, 3));

    [Fact]
    public void ShouldApply_SwitchedAwayAndBackToRecommend_ReturnsFalse()
        => Assert.False(RecommendRefreshResultGuard.ShouldApply(true, 3, 5));
}
