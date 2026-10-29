// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 一次推荐视频刷新落到列表上的结果.
/// </summary>
/// <typeparam name="T">视频条目类型.</typeparam>
/// <param name="Items">刷新后应展示的列表.</param>
/// <param name="AddedCount">本次增量刷新新增的条目数，普通重载为 0.</param>
public sealed record RecommendRefreshApplication<T>(
    IReadOnlyList<T> Items,
    int AddedCount);

/// <summary>
/// 推荐视频刷新结果的组装工具.
/// </summary>
/// <remarks>
/// 两个推荐页面的刷新流程都包含「合并或替换列表」与「记录新增条数」两步，
/// 规则完全相同，因此统一放在这里；服务调用、偏移量、缓存、快照与提示状态仍由各页面自行管理。
/// </remarks>
public static class RecommendRefreshApplier
{
    /// <summary>
    /// 组装一次刷新后应展示的列表与新增条数.
    /// </summary>
    /// <typeparam name="T">视频条目类型.</typeparam>
    /// <param name="current">当前已展示的列表.</param>
    /// <param name="incoming">本次刷新取到的新批次，允许为 null.</param>
    /// <param name="keySelector">取得视频资源 Id 的函数.</param>
    /// <param name="isIncremental">本次刷新是否为增量刷新.</param>
    /// <returns>刷新后应展示的列表与新增条数.</returns>
    /// <remarks>
    /// 非增量刷新直接使用新批次（无新批次时为空列表），新增条数为 0；
    /// 增量刷新经 <see cref="RecommendVideoMerger"/> 合并后返回新增条数。
    /// </remarks>
    public static RecommendRefreshApplication<T> Apply<T>(
        IReadOnlyList<T> current,
        IEnumerable<T>? incoming,
        Func<T, string> keySelector,
        bool isIncremental)
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        if (!isIncremental)
        {
            return new RecommendRefreshApplication<T>(incoming?.ToList() ?? [], 0);
        }

        var merged = RecommendVideoMerger.Merge(current, incoming, keySelector);
        return new RecommendRefreshApplication<T>(merged.Items, merged.AddedItems.Count);
    }
}
