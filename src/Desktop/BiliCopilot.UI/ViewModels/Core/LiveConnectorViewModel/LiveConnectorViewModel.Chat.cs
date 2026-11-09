// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.Concurrent;
using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播连接器的聊天归属（#197）：订阅 #196 的事件泵，把事件批量映射成有界聊天列表。
/// </summary>
/// <remarks>
/// 事件在弹幕泵线程触发，这里只入队（无锁、不触碰 UI）；UI 线程按固定节拍批量消费，
/// 因此不存在"每条消息一次调度"。列表只有在批量裁剪或醒目留言被删除时才整表重置，
/// 其余时候只追加一条。补屏在房间就绪后拉取最近弹幕，失败只记日志、不影响播放。
/// 屏蔽只做展示侧过滤，不发送、不送礼、不撤回。
/// </remarks>
public sealed partial class LiveConnectorViewModel
{
    private static readonly TimeSpan ChatTickInterval = TimeSpan.FromMilliseconds(120);
    private const int MaxChatEventsPerTick = 64;

    private readonly ConcurrentQueue<LiveDanmakuEvent> _chatQueue = new();
    private readonly object _chatGate = new();
    private LiveChatSession? _chatSession;
    private DispatcherQueueTimer? _chatTimer;
    private int _chatGeneration = -1;
    private string? _chatRoomId;
    private DateTimeOffset _lastEnterRoomAt = DateTimeOffset.MinValue;
    private IReadOnlyDictionary<string, LiveDanmakuEmote>? _emoteResources;
    private IReadOnlyDictionary<string, LiveTitleItem>? _titleResources;
    private int _wealthLogCount;
    private bool _isChatBackfillStarted;
    private bool _isChatHooked;

    /// <summary>
    /// 由聊天侧栏控件在加载时调用：订阅事件泵并启动批量消费节拍。
    /// </summary>
    internal void StartChat()
    {
        if (_isChatHooked)
        {
            return;
        }

        _isChatHooked = true;
        DanmakuEventReceived += OnChatEventReceived;
        DanmakuStateChanged += OnChatStateChanged;

        var dispatcher = this.Get<DispatcherQueue>();
        var timer = dispatcher.CreateTimer();
        timer.Interval = ChatTickInterval;
        timer.IsRepeating = true;
        timer.Tick += OnChatTimerTick;
        _chatTimer = timer;
        timer.Start();
        _ = LoadEmoteResourcesAsync();
        _ = LoadTitleResourcesAsync();
    }

    private async Task LoadTitleResourcesAsync()
    {
        if (_titleResources is { Count: > 0 })
        {
            return;
        }

        try
        {
            var items = await this.Get<Richasy.BiliKernel.Bili.Media.IPlayerService>()
                .GetLiveTitlesAsync()
                .ConfigureAwait(false);
            if (items.Count == 0)
            {
                return;
            }

            _titleResources = items
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            _logger.LogInformation("直播头衔表已加载");
        }
        catch (Exception ex)
        {
            // 头衔图标拿不到只影响标识展示，不影响聊天。
            _logger.LogWarning(ex, "加载直播头衔失败");
        }
    }

    private async Task LoadEmoteResourcesAsync()
    {
        var roomId = string.IsNullOrEmpty(_chatRoomId) ? Session.RoomId : _chatRoomId;
        if (string.IsNullOrEmpty(roomId) || _emoteResources is { Count: > 0 })
        {
            return;
        }

        try
        {
            var items = await this.Get<Richasy.BiliKernel.Bili.Media.IPlayerService>()
                .GetLiveEmoticonsAsync(roomId)
                .ConfigureAwait(false);
            if (items.Count == 0)
            {
                return;
            }

            var map = new Dictionary<string, LiveDanmakuEmote>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var emote = new LiveDanmakuEmote(item.Url, item.Width, item.Height, item.Unique);
                if (!string.IsNullOrEmpty(item.Text))
                {
                    map[item.Text.Trim('[', ']')] = emote;
                }

                if (!string.IsNullOrEmpty(item.Unique))
                {
                    map[item.Unique.Trim('[', ']')] = emote;
                }
            }

            _emoteResources = map;
            _logger.LogInformation("直播表情包已加载");
        }
        catch (Exception ex)
        {
            // 表情包拿不到只影响表情渲染，不影响聊天。
            _logger.LogWarning(ex, "加载直播间表情包失败");
        }
    }

    /// <summary>
    /// 由聊天侧栏控件在卸载时调用：退订并停止节拍；已加载的消息保留。
    /// </summary>
    internal void StopChat()
    {
        if (!_isChatHooked)
        {
            return;
        }

        _isChatHooked = false;
        DanmakuEventReceived -= OnChatEventReceived;
        DanmakuStateChanged -= OnChatStateChanged;
        if (_chatTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnChatTimerTick;
            _chatTimer = null;
        }
    }

    /// <summary>
    /// 更新屏蔽规则；只影响之后到达的消息。
    /// </summary>
    /// <param name="userIds">要屏蔽的用户 ID.</param>
    /// <param name="userNames">要屏蔽的昵称.</param>
    /// <param name="keywords">要屏蔽的关键词.</param>
    internal void SetChatBlockRules(
        IEnumerable<long>? userIds,
        IEnumerable<string>? userNames,
        IEnumerable<string>? keywords)
    {
        var session = GetOrCreateChatSession();
        session.UpdateBlockRules(userIds, userNames, keywords);
        UpdateChatBlockCounters();
    }

    private LiveChatSession GetOrCreateChatSession()
    {
        if (_chatSession is { } existing)
        {
            return existing;
        }

        lock (_chatGate)
        {
            _chatSession ??= new LiveChatSession();
            return _chatSession;
        }
    }

    private void OnChatEventReceived(object? sender, LiveDanmakuEvent liveEvent)
    {
        if (liveEvent is LiveDanmakuReceivedEvent or LiveSuperChatEvent or LiveSuperChatDeletedEvent
            or LiveConnectionStateChangedEvent or LiveRoomNotStartedEvent or LiveStartedEvent or LiveRoomCutOffEvent)
        {
            _chatQueue.Enqueue(liveEvent);
        }
    }

    private void OnChatStateChanged(object? sender, LiveDanmakuConnectionState state)
    {
        // 状态消息由事件流里的 LiveConnectionStateChangedEvent 承载，这里只兜底认证失败。
        if (state == LiveDanmakuConnectionState.Closed && DanmakuPump is null)
        {
            _chatQueue.Enqueue(new LiveConnectionStateChangedEvent(state, null, TimeProvider.GetUtcNow()));
        }
    }

    /// <summary>
    /// 把一次认证失败作为系统消息排进聊天（调用方已在 UI 线程）。
    /// </summary>
    private void EnqueueChatAuthenticationFailure(string? detail)
    {
        var now = TimeProvider.GetUtcNow();
        var result = GetOrCreateChatSession().ApplyAuthenticationFailure(detail, now);
        foreach (var message in result.Added)
        {
            ChatMessages.Append(CreateChatItem(message, now));
        }

        if (result.RequiresReset)
        {
            ReplaceChatItems(GetOrCreateChatSession(), now);
        }

        IsChatEmpty = ChatMessages.Count == 0;
    }

    private void OnChatTimerTick(DispatcherQueueTimer sender, object args)
    {
        SyncChatGeneration();

        var session = GetOrCreateChatSession();
        var now = TimeProvider.GetUtcNow();
        var added = new List<LiveChatMessage>(MaxChatEventsPerTick);
        var requiresReset = false;
        var trimmed = 0;

        for (var i = 0; i < MaxChatEventsPerTick && _chatQueue.TryDequeue(out var liveEvent); i++)
        {
            var result = session.Apply(liveEvent, now);
            // 醒目留言被删除时必须整表重建（要按标识移除）；普通裁剪只需从头部摘掉对应条数，
            // 否则每 50 条消息就要重建 500 个条目，长时间观看会明显卡顿。
            requiresReset |= result.Removed > 0;
            trimmed += result.Trimmed;
            foreach (var message in result.Added)
            {
                added.Add(message);
            }
        }

        var expired = session.ExpireSuperChats(now);
        requiresReset |= expired > 0;

        if (requiresReset)
        {
            ReplaceChatItems(session, now);
        }
        else
        {
            if (trimmed > 0)
            {
                ChatMessages.RemoveFromStart(trimmed);
            }

            foreach (var message in added)
            {
                AppendChatMessage(message, now);
            }
        }

        UpdateChatRemaining(now);
        UpdateChatBlockCounters();
        IsChatEmpty = ChatMessages.Count == 0;
        TryStartChatBackfill();
    }

    private void SyncChatGeneration()
    {
        var generation = Session.Generation;
        var roomId = Session.RoomId;
        if (generation == _chatGeneration && string.Equals(roomId, _chatRoomId, StringComparison.Ordinal))
        {
            return;
        }

        _chatGeneration = generation;
        _chatRoomId = roomId;
        _isChatBackfillStarted = false;
        // 进房时房号才确定；StartChat 里可能还是空串，这里补一次资源加载。
        _ = LoadEmoteResourcesAsync();
        _ = LoadTitleResourcesAsync();
        while (_chatQueue.TryDequeue(out _))
        {
        }

        GetOrCreateChatSession().Clear();
        ChatMessages.ClearAll();
        IsChatEmpty = true;
        UpdateChatBlockCounters();
    }

    private void ReplaceChatItems(LiveChatSession session, DateTimeOffset now)
    {
        var items = new List<LiveChatMessageItem>(session.Window.Count);
        foreach (var message in session.Window.Items)
        {
            items.Add(CreateChatItem(message, now));
        }

        ChatMessages.ReplaceAll(items);
    }

    private LiveChatMessageItem CreateChatItem(LiveChatMessage message, DateTimeOffset now)
        => new(message, GetChatSystemText, now, GetChatBadgeText, GetChatPriceText, _emoteResources, _titleResources);

    /// <summary>
    /// 按参考实现的准入思路处理上屏：同文案短窗合并、进房提示限速，避免刷屏把列表打爆。
    /// </summary>
    private void AppendChatMessage(LiveChatMessage message, DateTimeOffset now)
    {
        if (_wealthLogCount < 20 && message.WealthIconKey is { Length: > 0 } wealthKey)
        {
            _wealthLogCount++;
            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
            {
                var wealthName = message.UserName;
                var wealthLevel = message.WealthLevel ?? 0;
                _logger.LogInformation("WEALTH|{Name}|{Level}|{Key}", wealthName, wealthLevel, wealthKey);
            }
        }

        if (message.IsEnterRoom)
        {
            // 大房间每秒有大量进房事件；保留提示但限制到每秒最多一条。
            if (now - _lastEnterRoomAt < TimeSpan.FromSeconds(1))
            {
                return;
            }

            _lastEnterRoomAt = now;
            ChatMessages.Append(CreateChatItem(message, now));
            return;
        }

        if (message.Kind == LiveChatMessageKind.Danmaku
            && TryMergeRecentDanmaku(message, now))
        {
            return;
        }

        ChatMessages.Append(CreateChatItem(message, now));
    }

    private bool TryMergeRecentDanmaku(LiveChatMessage message, DateTimeOffset now)
    {
        // 只看最近的少量条目：刷屏通常是连续重复，不需要回扫整个窗口。
        var count = Math.Min(ChatMessages.Count, 12);
        for (var i = ChatMessages.Count - 1; i >= ChatMessages.Count - count; i--)
        {
            var item = ChatMessages[i];
            if (item.Kind != LiveChatMessageKind.Danmaku
                || !string.Equals(item.Text, message.Text, StringComparison.Ordinal))
            {
                continue;
            }

            if (now - item.Message.Timestamp <= TimeSpan.FromSeconds(2))
            {
                item.IncrementMerge();
                return true;
            }

            return false;
        }

        return false;
    }

    private string GetChatBadgeText(LiveChatBadgeKind kind)
    {
        var name = kind switch
        {
            LiveChatBadgeKind.Admin => StringNames.LiveChatBadgeAdmin,
            LiveChatBadgeKind.Governor => StringNames.LiveChatBadgeGovernor,
            LiveChatBadgeKind.Admiral => StringNames.LiveChatBadgeAdmiral,
            LiveChatBadgeKind.Captain => StringNames.LiveChatBadgeCaptain,
            _ => StringNames.LiveChatBadgeVip,
        };

        return ResourceToolkit.GetLocalizedString(name);
    }

    private static string GetChatPriceText(int price)
        => LiveChatSuperChatTime.FormatPrice(price, ResourceToolkit.GetLocalizedString(StringNames.LiveSuperChatPriceTemplate));

    private string GetChatSystemText(LiveChatSystemKind kind)
    {
        var name = kind switch
        {
            LiveChatSystemKind.Connecting => StringNames.LiveChatSystemConnecting,
            LiveChatSystemKind.Connected => StringNames.LiveChatSystemConnected,
            LiveChatSystemKind.Reconnecting => StringNames.LiveChatSystemReconnecting,
            LiveChatSystemKind.Disconnected => StringNames.LiveChatSystemDisconnected,
            LiveChatSystemKind.AuthenticationFailed => StringNames.LiveChatSystemAuthenticationFailed,
            LiveChatSystemKind.RoomNotStarted => StringNames.LiveChatSystemRoomNotStarted,
            LiveChatSystemKind.RoomStarted => StringNames.LiveChatSystemRoomStarted,
            _ => StringNames.LiveChatSystemRoomCutOff,
        };

        return ResourceToolkit.GetLocalizedString(name);
    }

    private void UpdateChatRemaining(DateTimeOffset now)
    {
        foreach (var item in ChatMessages)
        {
            if (item.IsSuperChat)
            {
                item.UpdateRemaining(now);
            }
        }
    }

    private void UpdateChatBlockCounters()
    {
        var counters = GetOrCreateChatSession().BlockCounters;
        ChatBlockCountersText = counters.Total == 0
            ? default
            : string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                ResourceToolkit.GetLocalizedString(StringNames.LiveChatBlockCountersTemplate),
                counters.ByUserId,
                counters.ByUserName,
                counters.ByKeyword,
                counters.Total);
    }

    private void TryStartChatBackfill()
    {
        if (_isChatBackfillStarted || Session.RoomId is not { Length: > 0 } roomId || !Session.IsLiving)
        {
            return;
        }

        _isChatBackfillStarted = true;
        IsChatBackfilling = true;
        _ = BackfillChatAsync(roomId, Session.Generation);
    }

    private async Task BackfillChatAsync(string roomId, int generation)
    {
        try
        {
            var messages = await this.Get<Richasy.BiliKernel.Bili.Media.IPlayerService>()
                .GetRecentLiveDanmakuAsync(new MediaIdentifier(roomId, null, null))
                .ConfigureAwait(false);
            if (messages.Count == 0)
            {
                return;
            }

            var dispatcher = this.Get<DispatcherQueue>();
            dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (!LiveChatBackfillGuard.IsCurrent(generation, _chatGeneration, Session.Generation))
                {
                    return;
                }

                var session = GetOrCreateChatSession();
                var now = TimeProvider.GetUtcNow();
                foreach (var danmaku in messages)
                {
                    var result = session.Apply(new LiveDanmakuReceivedEvent(danmaku, "BACKFILL", now), now);
                    if (result.RequiresReset)
                    {
                        ReplaceChatItems(session, now);
                    }
                    else
                    {
                        foreach (var message in result.Added)
                        {
                            ChatMessages.Append(CreateChatItem(message, now));
                        }
                    }
                }

                IsChatEmpty = ChatMessages.Count == 0;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "直播间 {RoomId} 补屏失败", roomId);
        }
        finally
        {
            var dispatcher = this.Get<DispatcherQueue>();
            dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => IsChatBackfilling = false);
        }
    }
}
