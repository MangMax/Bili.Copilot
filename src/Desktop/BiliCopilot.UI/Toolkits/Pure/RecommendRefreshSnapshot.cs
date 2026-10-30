// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 推荐视频刷新前的快照，用于「撤销刷新」.
/// </summary>
/// <typeparam name="T">视频条目类型.</typeparam>
/// <param name="Items">刷新前的列表内容，已与页面集合解耦.</param>
/// <param name="Offset">刷新前用于继续加载的偏移量.</param>
public sealed record RecommendRefreshSnapshot<T>(
    IReadOnlyList<T> Items,
    long Offset)
{
    /// <summary>
    /// 记录一份快照.
    /// </summary>
    /// <param name="items">刷新前的列表，允许为空或为 null.</param>
    /// <param name="offset">刷新前用于继续加载的偏移量.</param>
    /// <returns>与页面集合解耦的快照.</returns>
    /// <remarks>
    /// 必须复制内容：页面刷新时会 Clear 并重填同一个集合，
    /// 若快照持有集合引用，刷新后快照会被一并改写，撤销就恢复不回去了。
    /// </remarks>
    public static RecommendRefreshSnapshot<T> Capture(IEnumerable<T>? items, long offset)
        => new(items?.ToList() ?? [], offset);
}
