// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 一次窗口变更：新增一条、批量裁剪若干条，或需要整表重置。
/// </summary>
/// <param name="Added">本次新增的消息（裁剪或移除时为 null）.</param>
/// <param name="Trimmed">本次批量裁剪的条数.</param>
/// <param name="Removed">本次按标识移除的条数.</param>
public readonly record struct LiveChatWindowChange(
    LiveChatMessage? Added,
    int Trimmed = 0,
    int Removed = 0)
{
    /// <summary>
    /// Gets a value indicating whether 界面需要一次性重建列表.
    /// </summary>
    public bool RequiresReset => Trimmed > 0 || Removed > 0;
}

/// <summary>
/// 聊天列表窗口：保留最近约 <c>retention</c> 条，裁剪按 <c>slack</c> 为粒度整批进行。
/// </summary>
/// <remarks>
/// 列表始终落在 <c>[retention, retention + slack]</c> 区间内，只有跨越上界时才一次性裁回
/// <c>retention</c>，因此界面每 <c>slack</c> 条消息才需要一次整表重置，而不是每条消息一次。
/// 这样既满足"约 500 条"的有界要求，也避免了逐条头部删除造成的滚动抖动。
/// </remarks>
public sealed class LiveChatWindow
{
    /// <summary>
    /// 默认保留条数。
    /// </summary>
    public const int DefaultRetention = 500;

    /// <summary>
    /// 默认裁剪粒度。
    /// </summary>
    public const int DefaultSlack = 50;

    private readonly List<LiveChatMessage> _items;
    private readonly int _retention;
    private readonly int _slack;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatWindow"/> class.
    /// </summary>
    /// <param name="retention">保留条数.</param>
    /// <param name="slack">裁剪粒度.</param>
    public LiveChatWindow(int retention = DefaultRetention, int slack = DefaultSlack)
    {
        if (retention < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retention), "保留条数必须大于零。");
        }

        if (slack < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slack), "裁剪粒度不能为负数。");
        }

        _retention = retention;
        _slack = slack;
        _items = new List<LiveChatMessage>(retention + slack);
    }

    /// <summary>
    /// Gets 当前条数.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets 保留条数上限.
    /// </summary>
    public int Retention => _retention;

    /// <summary>
    /// Gets 当前窗口（按时间顺序）.
    /// </summary>
    public IReadOnlyList<LiveChatMessage> Items => _items;

    /// <summary>
    /// 追加一条消息；超过窗口上界时整批裁剪。
    /// </summary>
    /// <param name="message">消息.</param>
    /// <returns>本次变更.</returns>
    public LiveChatWindowChange Append(LiveChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _items.Add(message);

        var upperBound = _retention + _slack;
        if (_items.Count <= upperBound)
        {
            return new LiveChatWindowChange(message);
        }

        var trimmed = _items.Count - _retention;
        _items.RemoveRange(0, trimmed);
        return new LiveChatWindowChange(default, trimmed);
    }

    /// <summary>
    /// 按标识移除醒目留言。
    /// </summary>
    /// <param name="superChatIds">要移除的醒目留言标识.</param>
    /// <returns>本次变更.</returns>
    public LiveChatWindowChange RemoveSuperChat(IReadOnlyList<string>? superChatIds)
    {
        if (superChatIds is null || superChatIds.Count == 0 || _items.Count == 0)
        {
            return default;
        }

        var ids = new HashSet<string>(superChatIds.Where(p => !string.IsNullOrEmpty(p)), StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return default;
        }

        var removed = 0;
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            if (item.IsSuperChat && item.SuperChatId is { } id && ids.Contains(id))
            {
                _items.RemoveAt(i);
                removed++;
            }
        }

        return removed == 0 ? default : new LiveChatWindowChange(default, Removed: removed);
    }

    /// <summary>
    /// 清空窗口。
    /// </summary>
    public void Clear() => _items.Clear();
}
