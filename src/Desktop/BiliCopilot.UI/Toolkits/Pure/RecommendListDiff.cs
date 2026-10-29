// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 一次最小差异更新中要插入的条目及它的目标位置.
/// </summary>
/// <typeparam name="T">条目类型.</typeparam>
/// <param name="Index">插入后在列表中的目标索引.</param>
/// <param name="Item">要插入的条目.</param>
public sealed record RecommendListInsertion<T>(int Index, T Item);

/// <summary>
/// 从当前列表更新到目标列表所需的最小差异.
/// </summary>
/// <typeparam name="T">条目类型.</typeparam>
/// <param name="RemovedIndices">需要从当前列表删除的索引，按倒序排列，可直接依次删除.</param>
/// <param name="Insertions">需要插入的条目，按目标索引升序排列，可直接依次插入.</param>
public sealed record RecommendListDiffResult<T>(
    IReadOnlyList<int> RemovedIndices,
    IReadOnlyList<RecommendListInsertion<T>> Insertions);

/// <summary>
/// 推荐列表的最小差异计算工具.
/// </summary>
/// <remarks>
/// 刷新与撤销不再清空整个列表，而是只删除目标列表里没有的条目、只插入新增的条目，
/// 位置与顺序未变的条目原地保留，对应的卡片容器因此不会被销毁重建。
/// </remarks>
public static class RecommendListDiff
{
    /// <summary>
    /// 计算从当前列表更新到目标列表的最小差异.
    /// </summary>
    /// <typeparam name="T">条目类型.</typeparam>
    /// <param name="current">当前已展示的列表，允许为空或为 null.</param>
    /// <param name="target">刷新后应展示的目标列表，允许为空或为 null.</param>
    /// <param name="keySelector">取得条目唯一键的函数.</param>
    /// <returns>按位置描述的最小差异.</returns>
    /// <remarks>
    /// 以「当前列表与目标列表按键的最长公共子序列」作为保持不动的部分：
    /// 公共子序列里的条目既不删除也不重新插入，引用与顺序都保持原样；
    /// 其余当前条目删除，其余目标条目按目标索引插入，因此应用后顺序必然等于目标列表。
    /// 键相同的重复条目按出现次序匹配，不做额外去重。
    /// </remarks>
    public static RecommendListDiffResult<T> Compute<T>(
        IEnumerable<T>? current,
        IEnumerable<T>? target,
        Func<T, string> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        var currentItems = current?.ToList() ?? [];
        var targetItems = target?.ToList() ?? [];
        var currentCount = currentItems.Count;
        var targetCount = targetItems.Count;

        if (targetCount == 0)
        {
            var removals = new List<int>(currentCount);
            for (var i = currentCount - 1; i >= 0; i--)
            {
                removals.Add(i);
            }

            return new RecommendListDiffResult<T>(removals, []);
        }

        if (currentCount == 0)
        {
            var additions = new List<RecommendListInsertion<T>>(targetCount);
            for (var j = 0; j < targetCount; j++)
            {
                additions.Add(new RecommendListInsertion<T>(j, targetItems[j]));
            }

            return new RecommendListDiffResult<T>([], additions);
        }

        var currentKeys = new string[currentCount];
        for (var i = 0; i < currentCount; i++)
        {
            currentKeys[i] = keySelector(currentItems[i]);
        }

        var targetKeys = new string[targetCount];
        for (var j = 0; j < targetCount; j++)
        {
            targetKeys[j] = keySelector(targetItems[j]);
        }

        // 最长公共子序列长度表；推荐列表规模有限（一页已加载的条目），二维表足够。
        var lengths = new int[currentCount + 1, targetCount + 1];
        for (var i = currentCount - 1; i >= 0; i--)
        {
            for (var j = targetCount - 1; j >= 0; j--)
            {
                lengths[i, j] = string.Equals(currentKeys[i], targetKeys[j], StringComparison.Ordinal)
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        // 回溯出公共子序列，标记哪些当前条目与目标条目保持不动。
        var currentKept = new bool[currentCount];
        var targetKept = new bool[targetCount];
        var x = 0;
        var y = 0;
        while (x < currentCount && y < targetCount)
        {
            if (string.Equals(currentKeys[x], targetKeys[y], StringComparison.Ordinal))
            {
                currentKept[x] = true;
                targetKept[y] = true;
                x++;
                y++;
            }
            else if (lengths[x + 1, y] >= lengths[x, y + 1])
            {
                x++;
            }
            else
            {
                y++;
            }
        }

        var removedIndices = new List<int>();
        for (var i = currentCount - 1; i >= 0; i--)
        {
            if (!currentKept[i])
            {
                removedIndices.Add(i);
            }
        }

        var insertions = new List<RecommendListInsertion<T>>();
        for (var j = 0; j < targetCount; j++)
        {
            if (!targetKept[j])
            {
                insertions.Add(new RecommendListInsertion<T>(j, targetItems[j]));
            }
        }

        return new RecommendListDiffResult<T>(removedIndices, insertions);
    }

    /// <summary>
    /// 把差异应用到列表上.
    /// </summary>
    /// <typeparam name="T">条目类型.</typeparam>
    /// <param name="items">要更新的列表，通常是页面持有的 <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>.</param>
    /// <param name="diff">由 <see cref="Compute{T}"/> 得到的差异.</param>
    /// <remarks>
    /// 必须先按倒序删除、再按升序插入：删除不会让尚未处理的索引漂移，
    /// 插入时前面的目标位置已经就位，逐个 <c>Insert</c> 即可落在正确位置。
    /// </remarks>
    public static void Apply<T>(IList<T> items, RecommendListDiffResult<T> diff)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(diff);

        foreach (var index in diff.RemovedIndices)
        {
            items.RemoveAt(index);
        }

        foreach (var insertion in diff.Insertions)
        {
            items.Insert(insertion.Index, insertion.Item);
        }
    }

    /// <summary>
    /// 计算并应用差异，把列表更新为目标列表.
    /// </summary>
    /// <typeparam name="T">条目类型.</typeparam>
    /// <param name="items">要更新的列表，通常是页面持有的 <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>.</param>
    /// <param name="target">刷新后应展示的目标列表，允许为空或为 null.</param>
    /// <param name="keySelector">取得条目唯一键的函数.</param>
    public static void Apply<T>(IList<T> items, IEnumerable<T>? target, Func<T, string> keySelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        var diff = Compute(items, target, keySelector);
        Apply(items, diff);
    }
}
