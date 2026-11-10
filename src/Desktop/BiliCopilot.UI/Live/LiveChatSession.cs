// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 聊天计数快照。
/// </summary>
/// <param name="Received">收到的弹幕与醒目留言条数.</param>
/// <param name="Displayed">真正进入列表的条数.</param>
/// <param name="Blocked">被屏蔽的条数.</param>
/// <param name="SuperChats">收到的醒目留言条数.</param>
/// <param name="SuperChatsRemoved">被删除/过期的醒目留言条数.</param>
/// <param name="SystemMessages">产生的系统消息条数.</param>
/// <param name="Trimmed">因有界保留被裁剪的条数.</param>
public readonly record struct LiveChatCounters(
    int Received,
    int Displayed,
    int Blocked,
    int SuperChats,
    int SuperChatsRemoved,
    int SystemMessages,
    int Trimmed);

/// <summary>
/// 一次事件应用后的结果。
/// </summary>
/// <param name="AddedMessages">本次新增的可见消息.</param>
/// <param name="Removed">本次按标识移除的条数.</param>
/// <param name="Trimmed">本次批量裁剪的条数.</param>
public readonly record struct LiveChatApplyResult(
    IReadOnlyList<LiveChatMessage>? AddedMessages,
    int Removed = 0,
    int Trimmed = 0)
{
    /// <summary>
    /// Gets 本次新增的可见消息；没有新增时为空集合.
    /// </summary>
    public IReadOnlyList<LiveChatMessage> Added => AddedMessages ?? [];

    /// <summary>
    /// Gets a value indicating whether 界面需要一次性重建列表.
    /// </summary>
    public bool RequiresReset => Removed > 0 || Trimmed > 0;
}

/// <summary>
/// 一次直播观看的聊天会话：把内核事件映射成有界消息列表，并维护屏蔽与计数。
/// </summary>
/// <remarks>
/// 纯逻辑，不依赖 WinUI 与内核连接：输入是强类型事件与时间，输出是"新增了哪条 / 是否需要整表重置"，
/// 因此可以直接用假事件与假时钟测试。界面层只在 <see cref="LiveChatApplyResult.RequiresReset"/>
/// 为真时重建列表，其余时候追加一条，不会每条消息都重建整表。
/// 屏蔽规则在进入列表之前判定，被屏蔽的消息永远不会出现在列表里。
/// </remarks>
public sealed class LiveChatSession
{
    private readonly object _gate = new();
    private readonly LiveChatWindow _window;
    private readonly LiveChatBlockRules _blockRules;
    private long _sequence;
    private LiveDanmakuConnectionState? _lastState;
    private int _received;
    private int _displayed;
    private int _blocked;
    private int _superChats;
    private int _superChatsRemoved;
    private int _systemMessages;
    private int _trimmed;
    private DateTimeOffset? _lastEnterAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatSession"/> class.
    /// </summary>
    /// <param name="retention">保留条数.</param>
    /// <param name="slack">裁剪粒度.</param>
    /// <param name="blockRules">屏蔽规则；为空表示不屏蔽.</param>
    public LiveChatSession(
        int retention = LiveChatWindow.DefaultRetention,
        int slack = LiveChatWindow.DefaultSlack,
        LiveChatBlockRules? blockRules = null)
    {
        _window = new LiveChatWindow(retention, slack);
        _blockRules = blockRules ?? new LiveChatBlockRules();
    }

    /// <summary>
    /// Gets 当前聊天窗口.
    /// </summary>
    public LiveChatWindow Window => _window;

    /// <summary>
    /// Gets 当前屏蔽规则.
    /// </summary>
    public LiveChatBlockRules BlockRules => _blockRules;

    /// <summary>
    /// Gets 屏蔽计数.
    /// </summary>
    public LiveChatBlockCounters BlockCounters => _blockRules.Snapshot();

    /// <summary>
    /// Gets 聊天计数快照.
    /// </summary>
    public LiveChatCounters Counters
    {
        get
        {
            lock (_gate)
            {
                return new LiveChatCounters(
                    _received,
                    _displayed,
                    _blocked,
                    _superChats,
                    _superChatsRemoved,
                    _systemMessages,
                    _trimmed);
            }
        }
    }

    /// <summary>
    /// 整体替换屏蔽规则；已有计数保留。
    /// </summary>
    /// <param name="userIds">要屏蔽的用户 ID.</param>
    /// <param name="userNames">要屏蔽的昵称.</param>
    /// <param name="keywords">要屏蔽的关键词.</param>
    public void UpdateBlockRules(
        IEnumerable<long>? userIds,
        IEnumerable<string>? userNames,
        IEnumerable<string>? keywords)
    {
        lock (_gate)
        {
            _blockRules.Replace(userIds, userNames, keywords);
            _blocked += _window.RemoveWhere(message => message.Kind != LiveChatMessageKind.System
                && _blockRules.Evaluate(message.UserId, message.UserName, message.Text) != LiveChatBlockKind.None);
        }
    }

    /// <summary>
    /// 应用一个内核事件。
    /// </summary>
    /// <param name="liveEvent">内核事件.</param>
    /// <param name="now">当前时间；系统消息一律使用它，弹幕/醒目留言保留事件自身的接收时间.</param>
    /// <returns>本次变更.</returns>
    public LiveChatApplyResult Apply(LiveDanmakuEvent? liveEvent, DateTimeOffset now)
    {
        if (liveEvent is null)
        {
            return default;
        }

        switch (liveEvent)
        {
            case LiveDanmakuReceivedEvent received:
                return AppendDanmaku(received.Danmaku, received.ReceivedAt);
            case LiveUserEnteredEvent entered:
                return AppendEnter(entered.User, entered.ReceivedAt);
            case LiveSuperChatEvent superChat:
                return AppendSuperChat(superChat.SuperChat);
            case LiveSuperChatDeletedEvent deleted:
                return RemoveSuperChats(deleted.MessageIds);
            case LiveConnectionStateChangedEvent stateChanged:
                // 系统消息用调用方给的时间：假时钟必须能覆盖连接状态与房间事件的落点。
                return AppendSystem(stateChanged.State, stateChanged.Detail, now);
            default:
                var system = LiveChatSystemMessage.FromEvent(liveEvent, NextSequence(), now);
                return system is null ? default : Append(system);
        }
    }

    /// <summary>
    /// 按连接状态补充一条系统消息；状态未变化时不重复记录。
    /// </summary>
    /// <param name="state">连接状态.</param>
    /// <param name="detail">服务端说明.</param>
    /// <param name="timestamp">发生时间.</param>
    /// <returns>本次变更.</returns>
    public LiveChatApplyResult ApplyConnectionState(
        LiveDanmakuConnectionState state,
        string? detail,
        DateTimeOffset timestamp)
        => AppendSystem(state, detail, timestamp);

    /// <summary>
    /// 记录一次认证失败；认证失败不会重试，因此单独成一条系统消息。
    /// </summary>
    /// <param name="detail">失败说明.</param>
    /// <param name="timestamp">发生时间.</param>
    /// <returns>本次变更.</returns>
    public LiveChatApplyResult ApplyAuthenticationFailure(string? detail, DateTimeOffset timestamp)
    {
        var message = LiveChatSystemMessage.FromAuthenticationFailure(detail, NextSequence(), timestamp);
        return Append(message);
    }

    /// <summary>
    /// 移除已过结束时间的醒目留言。
    /// </summary>
    /// <param name="now">当前时间.</param>
    /// <returns>移除条数.</returns>
    public int ExpireSuperChats(DateTimeOffset now)
    {
        List<string>? expired = null;
        lock (_gate)
        {
            foreach (var item in _window.Items)
            {
                if (item.IsSuperChat
                    && item.SuperChatId is { } id
                    && LiveChatSuperChatTime.IsExpired(item.EndTime, now))
                {
                    (expired ??= []).Add(id);
                }
            }
        }

        if (expired is null)
        {
            return 0;
        }

        var result = RemoveSuperChats(expired);
        return result.Removed;
    }

    /// <summary>
    /// 清空列表与状态；计数保留。
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _window.Clear();
            _lastState = default;
            _lastEnterAt = null;
        }
    }

    private LiveChatApplyResult AppendDanmaku(LiveDanmakuInformation danmaku, DateTimeOffset timestamp)
    {
        if (danmaku is null)
        {
            return default;
        }

        lock (_gate)
        {
            _received++;
            if (_blockRules.Evaluate(danmaku.UserId, danmaku.UserName, danmaku.Text) != LiveChatBlockKind.None)
            {
                _blocked++;
                return default;
            }

            var message = LiveChatMessage.FromDanmaku(danmaku, NextSequence(), timestamp);
            var items = _window.Items;
            for (var i = items.Count - 1; i >= Math.Max(0, items.Count - 12); i--)
            {
                var previous = items[i];
                if (previous.Kind == LiveChatMessageKind.Danmaku
                    && previous.UserId == message.UserId
                    && previous.UserName == message.UserName
                    && previous.Text == message.Text
                    && timestamp >= previous.Timestamp
                    && timestamp - previous.Timestamp <= TimeSpan.FromSeconds(2))
                {
                    previous.Merge();
                    return default;
                }
            }

            return AppendCore(message);
        }
    }

    private LiveChatApplyResult AppendEnter(LiveUserEnteredInformation user, DateTimeOffset timestamp)
    {
        if (user is null || string.IsNullOrEmpty(user.UserName))
        {
            return default;
        }

        lock (_gate)
        {
            if (_blockRules.Evaluate(user.UserId, user.UserName, string.Empty) != LiveChatBlockKind.None
                || (_lastEnterAt is { } last && timestamp - last < TimeSpan.FromSeconds(1)))
            {
                return default;
            }

            _lastEnterAt = timestamp;
            return AppendCore(LiveChatMessage.FromEnter(user, NextSequence(), timestamp));
        }
    }

    private LiveChatApplyResult AppendSuperChat(LiveSuperChatInformation superChat)
    {
        if (superChat is null)
        {
            return default;
        }

        lock (_gate)
        {
            _received++;
            _superChats++;
            if (_blockRules.Evaluate(superChat.UserId, superChat.UserName, superChat.Message) != LiveChatBlockKind.None)
            {
                _blocked++;
                return default;
            }

            var message = LiveChatMessage.FromSuperChat(superChat, NextSequence());
            return AppendCore(message);
        }
    }

    private LiveChatApplyResult RemoveSuperChats(IReadOnlyList<string>? ids)
    {
        lock (_gate)
        {
            var change = _window.RemoveSuperChat(ids);
            if (change.Removed == 0)
            {
                return default;
            }

            _superChatsRemoved += change.Removed;
            return new LiveChatApplyResult(Array.Empty<LiveChatMessage>(), change.Removed, change.Trimmed);
        }
    }

    private LiveChatApplyResult AppendSystem(
        LiveDanmakuConnectionState state,
        string? detail,
        DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            if (_lastState == state)
            {
                return default;
            }

            _lastState = state;
            var message = LiveChatSystemMessage.FromConnectionState(state, detail, NextSequence(), timestamp);
            return AppendCore(message);
        }
    }

    private LiveChatApplyResult Append(LiveChatMessage message)
    {
        lock (_gate)
        {
            return AppendCore(message);
        }
    }

    private LiveChatApplyResult AppendCore(LiveChatMessage message)
    {
        var change = _window.Append(message);
        _trimmed += change.Trimmed;
        _displayed++;
        if (message.Kind == LiveChatMessageKind.System)
        {
            _systemMessages++;
        }

        return new LiveChatApplyResult(new[] { message }, Trimmed: change.Trimmed);
    }

    private long NextSequence() => ++_sequence;
}
