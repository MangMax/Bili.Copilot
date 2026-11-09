// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;
using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播连接器的弹幕归属：一次观看一个事件泵、一个准入队列与一个批量注入循环。
/// </summary>
/// <remarks>
/// 连接开始于房间详情成功之后，停止于关闭/换房；注入只调用共用的
/// <see cref="DanmakuRenderViewModel.AddDanmaku(string)"/>，不新写渲染器。
/// 上屏在固定节拍里批量投递到 UI 线程（每个节拍最多一次调度），不存在逐条调度。
/// 扩展接缝：#197 订阅 <see cref="DanmakuPump"/> 的 <see cref="LiveEventPump.EventReceived"/> 构建聊天流；
/// #198 订阅同一事件流与 <see cref="LiveEventPump.StateChanged"/> 驱动房间状态与连接提示。
/// </remarks>
public sealed partial class LiveConnectorViewModel
{
    private const int DanmakuEventCapacity = LiveEventPump.DefaultEventCapacity;

    private readonly object _danmakuGate = new();
    private LiveEventPump? _danmakuPump;
    private LiveDanmakuAdmission? _danmakuAdmission;
    private CancellationTokenSource? _danmakuSource;
    private Task? _danmakuTask;
    private int _danmakuGeneration;
    private bool _isLiveDanmakuHeld;

    /// <summary>
    /// Gets 当前观看的弹幕事件泵；#197/#198 可订阅强类型事件与连接状态。
    /// </summary>
    internal LiveEventPump? DanmakuPump => _danmakuPump;

    /// <summary>
    /// Gets 关键词高亮判定；由 <see cref="RefreshKeywordHighlight"/> 按设置回填，
    /// 为空表示没有配置关键词（#196 的高亮层由此生效）。
    /// </summary>
    internal Func<string, bool>? DanmakuKeywordHighlight { get; private set; }

    /// <summary>
    /// Gets 当前生效的关键词匹配器；未配置时为空匹配器.
    /// </summary>
    internal LiveDanmakuKeywordMatcher KeywordHighlight { get; private set; } = LiveDanmakuKeywordMatcher.Empty;

    /// <summary>
    /// 按设置重新读取关键词；进入房间时与设置变化时调用，只影响之后到达的弹幕。
    /// </summary>
    internal void RefreshKeywordHighlight()
    {
        var keywords = SettingsToolkit.ReadLocalSetting(SettingNames.LiveDanmakuHighlightKeywords, string.Empty);
        KeywordHighlight = LiveDanmakuKeywordMatcher.Parse(keywords);
        DanmakuKeywordHighlight = KeywordHighlight.Count == 0 ? null : KeywordHighlight.IsMatch;
    }

    /// <summary>
    /// Gets 当前弹幕连接状态；未连接时为已关闭。
    /// </summary>
    public LiveDanmakuConnectionState DanmakuConnectionState
        => _danmakuPump?.State ?? LiveDanmakuConnectionState.Closed;

    /// <summary>
    /// 强类型弹幕事件转发（#197 聊天/#198 房间状态）：换房后重新订阅仍然有效。
    /// </summary>
    /// <remarks>在事件泵线程触发；订阅者必须自行切回 UI 线程且不得抛出。</remarks>
    internal event EventHandler<LiveDanmakuEvent>? DanmakuEventReceived;

    /// <summary>
    /// 弹幕连接状态转发（#198 系统提示）。
    /// </summary>
    /// <remarks>在事件泵线程触发；订阅者必须自行切回 UI 线程。</remarks>
    internal event EventHandler<LiveDanmakuConnectionState>? DanmakuStateChanged;

    private async Task StartDanmakuAsync(string roomId)
    {
        await StopDanmakuAsync();
        // 关键词高亮只在进入房间时读一次设置，避免每条弹幕都读本地设置。
        RefreshKeywordHighlight();

        if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
        {
            var tokenUid = this.Get<Richasy.BiliKernel.Bili.Authorization.IBiliTokenResolver>().GetToken()?.UserId ?? 0;
            var cookies = this.Get<Richasy.BiliKernel.Bili.Authorization.IBiliCookiesResolver>().GetCookies();
            cookies.TryGetValue("DedeUserID", out var dedeUserId);
            _logger.LogInformation("LIVE-WS tokenUid={TokenUid} cookieUid={CookieUid}", tokenUid, dedeUserId ?? "none");
        }

        var generation = Interlocked.Increment(ref _danmakuGeneration);
        var admission = new LiveDanmakuAdmission(LiveDanmakuAdmissionOptions.Default);
        var pump = new LiveEventPump(this.Get<ILiveDanmakuService>(), DanmakuEventCapacity);
        var injector = new LiveDanmakuInjector(
            admission,
            batch => InjectDanmakuBatch(batch, generation),
            text => DanmakuKeywordHighlight?.Invoke(text) == true);
        pump.StateChanged += OnDanmakuStateChanged;
        pump.EventReceived += OnDanmakuPumpEventReceived;

        lock (_danmakuGate)
        {
            _danmakuAdmission = admission;
            _danmakuPump = pump;
        }

        try
        {
            if (!await pump.StartAsync(roomId))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "直播间 {RoomId} 弹幕连接失败", roomId);
            if (LiveDanmakuAuthFailure.IsAuthenticationFailure(ex))
            {
                // 认证失败不会重试（#192）：必须成为一条聊天系统消息 + 连接文案，而不是静默断开。
                ReportDanmakuAuthenticationFailure(LiveDanmakuAuthFailure.GetDetail(ex));
            }

            return;
        }

        if (generation != Volatile.Read(ref _danmakuGeneration))
        {
            // 连接期间已经关闭或换房：别把新连接挂到旧会话上。
            await pump.StopAsync();
            return;
        }

        var source = new CancellationTokenSource();
        lock (_danmakuGate)
        {
            if (generation != Volatile.Read(ref _danmakuGeneration))
            {
                source.Dispose();
                return;
            }

            _danmakuSource = source;
            _danmakuTask = injector.RunAsync(pump.Events, source.Token);
        }
    }

    /// <summary>
    /// 停止弹幕事件泵与注入循环；关闭/换房时调用，可重复调用。
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    private async Task StopDanmakuAsync()
    {
        LiveEventPump? pump;
        CancellationTokenSource? source;
        Task? task;
        lock (_danmakuGate)
        {
            pump = _danmakuPump;
            source = _danmakuSource;
            task = _danmakuTask;
            _danmakuPump = null;
            _danmakuSource = null;
            _danmakuTask = null;
            _danmakuAdmission = null;
        }

        Interlocked.Increment(ref _danmakuGeneration);
        if (pump is null && source is null)
        {
            return;
        }

        if (source is not null)
        {
            await source.CancelAsync();
        }

        if (task is not null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "停止直播弹幕注入失败");
            }
        }

        if (pump is not null)
        {
            pump.StateChanged -= OnDanmakuStateChanged;
            pump.EventReceived -= OnDanmakuPumpEventReceived;
            try
            {
                await pump.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "停止直播弹幕连接失败");
            }
        }

        source?.Dispose();
        DanmakuConnectionText = ResourceToolkit.GetLocalizedString(StringNames.LiveDanmakuClosed);
        DanmakuCountersText = default;
    }

    private void StartDanmakuInBackground(string roomId)
        => _ = ObserveStartDanmakuAsync(roomId);

    private async Task ObserveStartDanmakuAsync(string roomId)
    {
        try
        {
            await StartDanmakuAsync(roomId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "启动直播弹幕失败");
        }
    }

    private void StopDanmakuInBackground()
        => _ = ObserveStopDanmakuAsync();

    private async Task ObserveStopDanmakuAsync()
    {
        try
        {
            await StopDanmakuAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "停止直播弹幕失败");
        }
    }

    /// <summary>
    /// 把认证失败写进聊天系统消息与连接文案（UI 线程）。
    /// </summary>
    private void ReportDanmakuAuthenticationFailure(string? detail)
    {
        var dispatcher = this.Get<DispatcherQueue>();
        dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            var text = ResourceToolkit.GetLocalizedString(StringNames.LiveChatSystemAuthenticationFailed);
            DanmakuConnectionText = string.IsNullOrEmpty(detail) ? text : $"{text}（{detail}）";
            EnqueueChatAuthenticationFailure(detail);
        });
    }

    private void OnDanmakuPumpEventReceived(object? sender, LiveDanmakuEvent liveEvent)
        => DanmakuEventReceived?.Invoke(this, liveEvent);

    private void OnDanmakuStateChanged(object? sender, LiveDanmakuConnectionState state)
    {
        DanmakuStateChanged?.Invoke(this, state);
        var detail = _danmakuPump?.StateDetail;
        var connectionName = GetConnectionName(state);
        var isAuthFailure = state == LiveDanmakuConnectionState.Closed && LiveDanmakuAuthFailure.IsAuthenticationDetail(detail);
        var dispatcher = this.Get<DispatcherQueue>();
        dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            var text = ResourceToolkit.GetLocalizedString(isAuthFailure ? StringNames.LiveChatSystemAuthenticationFailed : connectionName);
            DanmakuConnectionText = string.IsNullOrEmpty(detail) ? text : $"{text}（{detail}）";
            if (isAuthFailure)
            {
                // 连接以认证说明关闭：同样补一条聊天系统消息，避免只留下"已断开"。
                EnqueueChatAuthenticationFailure(detail);
            }

            UpdateDanmakuCounters();
        });
    }

    private void InjectDanmakuBatch(IReadOnlyList<string> batch, int generation)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var dispatcher = this.Get<DispatcherQueue>();
        dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (generation != Volatile.Read(ref _danmakuGeneration) || Parent.Window?.IsClosed != false)
            {
                return;
            }

            // 直播暂停期间持续到达的弹幕不再入队：恢复播放时按最新位置继续，
            // 否则积压的一批会同时从同一位置涌入。
            if (!Parent.Player.IsPlaying)
            {
                if (!_isLiveDanmakuHeld)
                {
                    _isLiveDanmakuHeld = true;
                    Parent.Danmaku.ClearDanmaku();
                }

                UpdateDanmakuCounters();
                return;
            }

            _isLiveDanmakuHeld = false;
            foreach (var text in batch)
            {
                Parent.Danmaku.AddDanmaku(text);
            }

            UpdateDanmakuCounters();
        });
    }

    private void UpdateDanmakuCounters()
    {
        var counters = _danmakuAdmission?.Snapshot();
        DanmakuCountersText = counters is { } value
            ? string.Format(
                CultureInfo.CurrentCulture,
                ResourceToolkit.GetLocalizedString(StringNames.LiveDanmakuCountersTemplate),
                value.Received,
                value.Shown,
                value.Merged,
                value.Blocked,
                value.RateDropped,
                value.QueueDropped,
                value.TimeoutDropped)
            : default;
    }

    private static StringNames GetConnectionName(LiveDanmakuConnectionState state)
        => state switch
        {
            LiveDanmakuConnectionState.Connecting => StringNames.LiveDanmakuConnecting,
            LiveDanmakuConnectionState.Connected => StringNames.LiveDanmakuConnected,
            LiveDanmakuConnectionState.Reconnecting => StringNames.LiveDanmakuReconnecting,
            _ => StringNames.LiveDanmakuClosed,
        };
}
