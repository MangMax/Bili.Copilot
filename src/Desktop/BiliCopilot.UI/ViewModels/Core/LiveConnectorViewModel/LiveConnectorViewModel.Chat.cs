// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Pages.Overlay;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models.User;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Base;
using Windows.ApplicationModel.DataTransfer;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播连接器的聊天归属（#197）：订阅 #196 的事件泵，把事件批量映射成有界聊天列表。
/// </summary>
/// <remarks>
/// 事件在弹幕泵线程触发，这里只入队（无锁、不触碰 UI）；UI 线程按固定节拍批量消费，
/// 列表投影保留存活的消息对象，重复合并、裁剪与删除统一由会话模型决定。补屏在房间就绪后拉取最近弹幕，失败只记日志、不影响播放。
/// 屏蔽只做展示侧过滤，不发送、不送礼、不撤回。
/// </remarks>
public sealed partial class LiveConnectorViewModel
{
    private static readonly TimeSpan ChatTickInterval = TimeSpan.FromMilliseconds(120);
    private const int MaxChatEventsPerTick = 64;

    private readonly LiveChatEventBuffer _chatQueue = new();
    private readonly object _chatGate = new();
    private LiveChatSession? _chatSession;
    private DispatcherQueueTimer? _chatTimer;
    private int _chatGeneration = -1;
    private string? _chatRoomId;
    private long _lastCountdownSecond = -1;
    private readonly List<LiveChatMessageItem> _activeSuperChats = [];
    private IReadOnlyDictionary<string, LiveDanmakuEmote>? _emoteResources;
    private IReadOnlyDictionary<string, LiveTitleItem>? _titleResources;
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
        SyncChatGeneration();
        timer.Start();
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
                .GetLiveTitlesAsync();
            if (items.Count == 0)
            {
                return;
            }

            _titleResources = items
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (var row in ChatMessages)
            {
                row.UpdateTitles(_titleResources);
            }

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

        var generation = Session.Generation;
        try
        {
            var items = await this.Get<Richasy.BiliKernel.Bili.Media.IPlayerService>()
                .GetLiveEmoticonsAsync(roomId);
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

            if (generation != Session.Generation || roomId != Session.RoomId)
            {
                return;
            }

            _emoteResources = map;
            foreach (var row in ChatMessages)
            {
                row.UpdateEmotes(_emoteResources);
            }
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

    [ObservableProperty]
    public partial string ChatBlockedUserIds { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChatBlockedUserNames { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChatBlockedKeywords { get; set; } = string.Empty;

    [RelayCommand]
    private void ApplyChatBlockRules()
    {
        var session = GetOrCreateChatSession();
        session.UpdateBlockRules(LiveChatBlockInput.UserIds(ChatBlockedUserIds),
            LiveChatBlockInput.Lines(ChatBlockedUserNames), LiveChatBlockInput.Lines(ChatBlockedKeywords));
        ReplaceChatItems(session, TimeProvider.GetUtcNow());
        IsChatEmpty = ChatMessages.Count == 0;
        UpdateChatBlockCounters();
        Parent.Window?.ShowTip(ResourceToolkit.GetLocalizedString(StringNames.LiveChatBlockRulesApplied), InfoType.Success);
    }

    internal void BlockChatUser(long userId)
    {
        if (userId <= 0) return;
        ChatBlockedUserIds = string.Join(Environment.NewLine,
            LiveChatBlockInput.UserIds(ChatBlockedUserIds).Append(userId).Distinct());
        ApplyChatBlockRules();
    }

    internal void BlockChatText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        ChatBlockedKeywords = LiveChatBlockInput.Add(ChatBlockedKeywords, text);
        ApplyChatBlockRules();
    }

    internal void CopyChatText(string text)
    {
        var data = new DataPackage();
        data.SetText(text);
        Clipboard.SetContent(data);
        Parent.Window?.ShowTip(ResourceToolkit.GetLocalizedString(StringNames.Copied), InfoType.Success);
    }

    internal void OpenChatUser(LiveChatMessage message)
    {
        if (message.UserId <= 0) return;
        var profile = new UserProfile(message.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture), message.UserName, null);
        this.Get<NavigationViewModel>().NavigateToOver(typeof(UserSpacePage), profile);
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
        _chatQueue.Enqueue(liveEvent);
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
        var session = GetOrCreateChatSession();
        session.ApplyAuthenticationFailure(detail, now);
        ReplaceChatItems(session, now);

        IsChatEmpty = ChatMessages.Count == 0;
    }

    private void OnChatTimerTick(DispatcherQueueTimer sender, object args)
    {
        SyncChatGeneration();

        var session = GetOrCreateChatSession();
        var now = TimeProvider.GetUtcNow();
        var changed = false;
        for (var i = 0; i < MaxChatEventsPerTick && _chatQueue.TryDequeue(out var liveEvent); i++)
        {
            session.Apply(liveEvent, now);
            changed = true;
        }

        var second = now.ToUnixTimeSeconds();
        if (second != _lastCountdownSecond)
        {
            _lastCountdownSecond = second;
            changed |= session.ExpireSuperChats(now) > 0;
            UpdateChatRemaining(now);
        }

        if (changed)
        {
            ReplaceChatItems(session, now);
        }

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
        _emoteResources = null;
        // 进房时房号才确定；StartChat 里可能还是空串，这里补一次资源加载。
        _ = LoadEmoteResourcesAsync();
        _ = LoadTitleResourcesAsync();
        _chatQueue.Clear();

        GetOrCreateChatSession().Clear();
        ChatMessages.ClearAll();
        _activeSuperChats.Clear();
        IsChatEmpty = true;
        UpdateChatBlockCounters();
    }

    private void ReplaceChatItems(LiveChatSession session, DateTimeOffset now)
    {
        LiveChatProjection.Synchronize(ChatMessages, session.Window.Items, message => CreateChatItem(message, now));
        _activeSuperChats.Clear();
        _activeSuperChats.AddRange(ChatMessages.Where(item => item.IsSuperChat));
    }

    private LiveChatMessageItem CreateChatItem(LiveChatMessage message, DateTimeOffset now)
        => new(this, message, GetChatSystemText, now, GetChatBadgeText, GetChatPriceText, _emoteResources, _titleResources);

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
        foreach (var item in _activeSuperChats)
        {
            item.UpdateRemaining(now);
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
                .GetRecentLiveDanmakuAsync(new MediaIdentifier(roomId, null, null));
            if (messages.Count == 0)
            {
                return;
            }

            if (!LiveChatBackfillGuard.IsCurrent(generation, _chatGeneration, Session.Generation))
            {
                return;
            }

            var session = GetOrCreateChatSession();
            var now = TimeProvider.GetUtcNow();
            foreach (var danmaku in messages)
            {
                session.Apply(new LiveDanmakuReceivedEvent(danmaku, "BACKFILL", now), now);
            }

            ReplaceChatItems(session, now);
            IsChatEmpty = ChatMessages.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "直播间 {RoomId} 补屏失败", roomId);
        }
        finally
        {
            if (LiveChatBackfillGuard.IsCurrent(generation, _chatGeneration, Session.Generation))
            {
                IsChatBackfilling = false;
            }
        }
    }
}
