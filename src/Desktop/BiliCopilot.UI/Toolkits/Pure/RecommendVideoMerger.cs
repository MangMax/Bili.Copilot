// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 推荐视频合并结果.
/// </summary>
/// <typeparam name="T">视频条目类型.</typeparam>
/// <param name="Items">合并后的完整列表.</param>
/// <param name="AddedItems">本次合并新增的条目，即新批次中原本不在当前列表里的那些.</param>
public sealed record RecommendVideoMergeResult<T>(
    IReadOnlyList<T> Items,
    IReadOnlyList<T> AddedItems);

/// <summary>
/// 推荐视频合并工具.
/// </summary>
/// <remarks>
/// 增量刷新时，新一批推荐按原顺序前插，当前列表中与新批次重复的条目被移除，
/// 其余条目保持原相对顺序排在后面，并以视频资源 Id 作为去重键。
/// 当前列表内部的重复条目原样保留：本需求不做全列表去重，增量刷新后继续下滑
/// 也可能偶发与旧内容重复，均属可接受行为。
/// </remarks>
public static class RecommendVideoMerger
{
    /// <summary>
    /// 将新一批推荐与当前列表合并.
    /// </summary>
    /// <typeparam name="T">视频条目类型.</typeparam>
    /// <param name="current">当前已展示的列表，允许为空或为 null.</param>
    /// <param name="incoming">新获取的推荐批次，允许为空或为 null.</param>
    /// <param name="keySelector">取得视频资源 Id 的函数.</param>
    /// <returns>合并结果；新批次中重复的条目只保留一条.</returns>
    /// <remarks>
    /// 新批次为 null 或空时不改动当前列表（包括其中的重复条目），新增为空。
    /// </remarks>
    public static RecommendVideoMergeResult<T> Merge<T>(
        IEnumerable<T>? current,
        IEnumerable<T>? incoming,
        Func<T, string> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        var currentItems = current?.ToList() ?? [];
        var incomingItems = incoming?.ToList() ?? [];

        // 没有新内容时原样返回当前列表，连其中的重复条目也不动。
        if (incomingItems.Count == 0)
        {
            return new RecommendVideoMergeResult<T>(currentItems, []);
        }

        var items = new List<T>(currentItems.Count + incomingItems.Count);
        var addedItems = new List<T>();
        var incomingKeys = new HashSet<string>(StringComparer.Ordinal);
        var currentKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in currentItems)
        {
            _ = currentKeys.Add(keySelector(item));
        }

        foreach (var item in incomingItems)
        {
            var key = keySelector(item);
            if (!incomingKeys.Add(key))
            {
                continue;
            }

            items.Add(item);
            if (!currentKeys.Contains(key))
            {
                addedItems.Add(item);
            }
        }

        foreach (var item in currentItems)
        {
            if (incomingKeys.Contains(keySelector(item)))
            {
                continue;
            }

            items.Add(item);
        }

        return new RecommendVideoMergeResult<T>(items, addedItems);
    }
}
