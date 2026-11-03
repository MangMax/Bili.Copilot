// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 把两路推荐流交错合并成一路.
/// </summary>
/// <remarks>
/// 合并模式同时请求手机端与网页端：同一支视频可能两边都推荐，
/// 按唯一键去重并保留首次出现的位置，顺序稳定可复现，便于列表差异计算。
/// </remarks>
public static class RecommendFeedMerger
{
    /// <summary>
    /// 交错合并两路推荐流.
    /// </summary>
    /// <typeparam name="T">条目类型.</typeparam>
    /// <param name="mobile">手机端推荐.</param>
    /// <param name="web">网页端推荐.</param>
    /// <param name="keySelector">取得条目唯一键的函数.</param>
    /// <returns>去重后的合并结果；任一路为空时直接返回另一路.</returns>
    public static IReadOnlyList<T> Merge<T>(
        IReadOnlyList<T>? mobile,
        IReadOnlyList<T>? web,
        Func<T, string> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        var mobileItems = mobile ?? [];
        var webItems = web ?? [];
        if (mobileItems.Count == 0)
        {
            return webItems;
        }

        if (webItems.Count == 0)
        {
            return mobileItems;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<T>(mobileItems.Count + webItems.Count);
        var length = Math.Max(mobileItems.Count, webItems.Count);
        for (var i = 0; i < length; i++)
        {
            if (i < mobileItems.Count)
            {
                Append(mobileItems[i], seen, merged, keySelector);
            }

            if (i < webItems.Count)
            {
                Append(webItems[i], seen, merged, keySelector);
            }
        }

        return merged;
    }

    private static void Append<T>(T item, HashSet<string> seen, List<T> merged, Func<T, string> keySelector)
    {
        if (seen.Add(keySelector(item)))
        {
            merged.Add(item);
        }
    }
}
