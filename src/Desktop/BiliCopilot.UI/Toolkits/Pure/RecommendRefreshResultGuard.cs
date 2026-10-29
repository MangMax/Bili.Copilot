// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 分区页推荐子分区刷新结果的落地判定.
/// </summary>
/// <remarks>
/// 刷新请求在路上时，用户可能切到别的子分区、切换排序、切换主分区页签或再次刷新，
/// 这些动作都会让列表代次递增。请求返回时代次已变，说明这批结果不属于当前列表。
/// 只比对「现在是不是推荐子分区」不够：切走再切回推荐子分区后它仍然是推荐子分区，
/// 但列表已经被别的动作改过，旧请求的结果不能覆盖它。
/// </remarks>
public static class RecommendRefreshResultGuard
{
    /// <summary>
    /// 判断一次刷新结果是否仍可写入当前视图模型.
    /// </summary>
    /// <param name="isRecommend">当前是否仍停留在推荐子分区.</param>
    /// <param name="generation">发起请求时的代次.</param>
    /// <param name="currentGeneration">当前代次.</param>
    /// <returns>可以应用结果时返回 <c>true</c>.</returns>
    public static bool ShouldApply(bool isRecommend, int generation, int currentGeneration)
        => isRecommend && generation == currentGeneration;
}
