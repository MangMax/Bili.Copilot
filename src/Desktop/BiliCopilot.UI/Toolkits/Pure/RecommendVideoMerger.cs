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
    public static RecommendVideoMergeResult<T> Merge<T>(
        IEnumerable<T>? current,
        IEnumerable<T>? incoming,
        Func<T, string> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        var currentItems = current?.ToList() ?? [];
        var items = new List<T>(currentItems.Count);
        var addedItems = new List<T>();
        var incomingKeys = new HashSet<string>(StringComparer.Ordinal);
        var currentKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in currentItems)
        {
            _ = currentKeys.Add(keySelector(item));
        }

        if (incoming is not null)
        {
            foreach (var item in incoming)
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
        }

        var remainingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in currentItems)
        {
            var key = keySelector(item);
            if (incomingKeys.Contains(key) || !remainingKeys.Add(key))
            {
                continue;
            }

            items.Add(item);
        }

        return new RecommendVideoMergeResult<T>(items, addedItems);
    }
}
