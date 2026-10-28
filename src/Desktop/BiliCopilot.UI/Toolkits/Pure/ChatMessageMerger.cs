// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 聊天消息合并结果.
/// </summary>
/// <typeparam name="T">消息类型.</typeparam>
/// <param name="Items">合并并按时间顺序排列的完整列表.</param>
/// <param name="AddedItems">本次合并新增的条目，按时间顺序排列.</param>
public sealed record ChatMessageMergeResult<T>(
    IReadOnlyList<T> Items,
    IReadOnlyList<T> AddedItems);

/// <summary>
/// 聊天消息合并工具.
/// </summary>
/// <remarks>
/// 私信接口只能返回“最近 N 条”消息，无法指定偏移量。多取几页时后一页会包含前一页的消息，
/// 因此需要在展示层按消息标识合并去重，并保持时间顺序（消息标识即消息序号，随发送时间递增）。
/// </remarks>
public static class ChatMessageMerger
{
    /// <summary>
    /// 合并两批消息，按标识去重并保持时间顺序.
    /// </summary>
    /// <typeparam name="T">消息类型.</typeparam>
    /// <param name="current">当前已展示的消息.</param>
    /// <param name="incoming">新获取的消息，允许为空或包含已存在的消息.</param>
    /// <param name="keySelector">取得消息唯一标识的函数.</param>
    /// <returns>合并结果；<paramref name="incoming"/> 中重复出现的标识只保留一条，后到的数据覆盖先前的数据.</returns>
    public static ChatMessageMergeResult<T> Merge<T>(
        IEnumerable<T>? current,
        IEnumerable<T>? incoming,
        Func<T, long> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        var byKey = new Dictionary<long, T>();
        var currentKeys = new HashSet<long>();

        if (current is not null)
        {
            foreach (var item in current)
            {
                var key = keySelector(item);
                byKey[key] = item;
                _ = currentKeys.Add(key);
            }
        }

        var addedKeys = new HashSet<long>();
        if (incoming is not null)
        {
            foreach (var item in incoming)
            {
                var key = keySelector(item);
                byKey[key] = item;
                if (!currentKeys.Contains(key))
                {
                    _ = addedKeys.Add(key);
                }
            }
        }

        var items = byKey.Values.OrderBy(keySelector).ToList();
        var addedItems = items.Where(p => addedKeys.Contains(keySelector(p))).ToList();
        return new ChatMessageMergeResult<T>(items, addedItems);
    }
}
