// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 一次推荐视频刷新落到列表上的结果.
/// </summary>
/// <typeparam name="T">视频条目类型.</typeparam>
/// <param name="Items">刷新后应展示的列表.</param>
/// <param name="Message">本次刷新的提示文案.</param>
public sealed record RecommendRefreshApplication<T>(
    IReadOnlyList<T> Items,
    string Message);

/// <summary>
/// 推荐视频刷新结果的组装工具.
/// </summary>
/// <remarks>
/// 两个推荐页面的刷新流程都包含「合并或替换列表」与「按新增条数组装提示文案」两步，
/// 规则完全相同，因此统一放在这里；服务调用、偏移量、缓存、快照与提示状态仍由各页面自行管理。
/// </remarks>
public static class RecommendRefreshApplier
{
    /// <summary>
    /// 组装一次刷新后应展示的列表与提示文案.
    /// </summary>
    /// <typeparam name="T">视频条目类型.</typeparam>
    /// <param name="current">当前已展示的列表.</param>
    /// <param name="incoming">本次刷新取到的新批次，允许为 null.</param>
    /// <param name="keySelector">取得视频资源 Id 的函数.</param>
    /// <param name="isIncremental">本次刷新是否为增量刷新.</param>
    /// <param name="addedTemplate">带 <c>{0}</c> 占位符的增量刷新文案模板.</param>
    /// <param name="reloadedTemplate">普通（清空重载）刷新文案.</param>
    /// <returns>刷新后应展示的列表与提示文案.</returns>
    /// <remarks>
    /// 非增量刷新直接使用新批次（无新批次时为空列表），提示文案固定为普通刷新文案；
    /// 增量刷新经 <see cref="RecommendVideoMerger"/> 合并，新增为 0 时提示文案回退为普通刷新文案。
    /// </remarks>
    public static RecommendRefreshApplication<T> Apply<T>(
        IReadOnlyList<T> current,
        IEnumerable<T>? incoming,
        Func<T, string> keySelector,
        bool isIncremental,
        string addedTemplate,
        string reloadedTemplate)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(addedTemplate);
        ArgumentNullException.ThrowIfNull(reloadedTemplate);

        if (!isIncremental)
        {
            return new RecommendRefreshApplication<T>(incoming?.ToList() ?? [], reloadedTemplate);
        }

        var merged = RecommendVideoMerger.Merge(current, incoming, keySelector);
        var message = RefreshResultMessageBuilder.Build(
            isIncremental: true,
            addedCount: merged.AddedItems.Count,
            addedTemplate,
            reloadedTemplate);

        return new RecommendRefreshApplication<T>(merged.Items, message);
    }
}
