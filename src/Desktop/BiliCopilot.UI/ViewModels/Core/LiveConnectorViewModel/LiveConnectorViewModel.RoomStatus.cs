// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播连接器的房间状态与实时事件接线（#198）。
/// </summary>
/// <remarks>
/// 只订阅连接器自己的 <see cref="LiveConnectorViewModel.DanmakuEventReceived"/>（#196 在事件泵线程转发），
/// 再由本类统一切回 UI 线程；不新增第二条内核连接，也不在面板里直连内核。
/// 下播/被切断由房间事件作为权威状态：终态下停止自动重连与自动刷新，并把
/// "返回"与"在浏览器打开"交给播放器视图模型；只有开播事件能恢复。
/// </remarks>
public sealed partial class LiveConnectorViewModel
{
    private readonly LiveRoomStatusTracker _roomStatus = new();
    private DispatcherQueueTimer? _uptimeTimer;
    private bool _isRoomStatusAttached;

    /// <summary>
    /// Gets 在线人数文案（按万/亿格式化）.
    /// </summary>
    [ObservableProperty]
    public partial string? OnlineCountText { get; set; }

    /// <summary>
    /// Gets 开播时长文案（分钟/小时）.
    /// </summary>
    [ObservableProperty]
    public partial string? UptimeText { get; set; }

    /// <summary>
    /// Gets 房间是否处于终态（已下播 / 被切断）.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRoomTerminal { get; set; }

    /// <summary>
    /// Gets 终态标题（主播已下播 / 直播被切断）；非终态为空.
    /// </summary>
    [ObservableProperty]
    public partial string? RoomStateTitle { get; set; }

    /// <summary>
    /// Gets 终态说明（服务端提示或兜底文案）；非终态为空.
    /// </summary>
    [ObservableProperty]
    public partial string? RoomStateDescription { get; set; }

    /// <summary>
    /// 进入房间时重置房间状态并开始接收房间事件。
    /// </summary>
    /// <param name="roomId">直播间标识.</param>
    internal void ResetRoomStatus(string? roomId)
    {
        _roomStatus.SetRoom(roomId);
        if (!_isRoomStatusAttached)
        {
            DanmakuEventReceived += OnRoomStatusEventReceived;
            _isRoomStatusAttached = true;
        }

        IsRoomTerminal = false;
        RoomStateTitle = default;
        RoomStateDescription = default;
        UptimeText = default;
        OnlineCountText = default;
        RefreshUptime();
        SeedOnlineCount();
        StartUptimeTimer();
    }

    /// <summary>
    /// 用房间详情初始化状态：未开播的房间直接进入终态，不等待后续事件。
    /// </summary>
    internal void ApplyRoomDetailStatus()
    {
        if (Session.Page is null)
        {
            return;
        }

        _roomStatus.ApplyRoomDetail(Session.IsLiving);
        SeedOnlineCount();
        RefreshUptime();
        if (!Session.IsLiving)
        {
            ApplyTerminalState(LiveRoomPhase.NotStarted, default);
        }
    }

    /// <summary>
    /// 离开房间时停止接收房间事件并停掉时长计时器。
    /// </summary>
    internal void DetachRoomStatus()
    {
        if (_isRoomStatusAttached)
        {
            DanmakuEventReceived -= OnRoomStatusEventReceived;
            _isRoomStatusAttached = false;
        }

        _uptimeTimer?.Stop();
        _uptimeTimer = null;
    }

    private void OnRoomStatusEventReceived(object? sender, LiveDanmakuEvent liveEvent)
    {
        var update = LiveRoomEventMapper.Map(liveEvent);
        if (update.Signal == LiveRoomStatusSignal.None)
        {
            return;
        }

        var dispatcher = this.Get<DispatcherQueue>();
        dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => ApplyRoomStatusUpdate(update));
    }

    private void ApplyRoomStatusUpdate(LiveRoomStatusUpdate update)
    {
        var roomId = _roomStatus.RoomId;
        switch (update.Signal)
        {
            case LiveRoomStatusSignal.Started:
                if (_roomStatus.ApplyStarted(roomId))
                {
                    ClearTerminalState();
                    // 开播后旧地址通常已失效：走同一次单飞刷新重新取址，不新建播放器。
                    RequestControlledReload();
                }

                break;
            case LiveRoomStatusSignal.NotStarted:
                if (_roomStatus.ApplyNotStarted(roomId))
                {
                    ApplyTerminalState(LiveRoomPhase.NotStarted, default);
                }

                break;
            case LiveRoomStatusSignal.CutOff:
                if (_roomStatus.ApplyCutOff(roomId))
                {
                    ApplyTerminalState(LiveRoomPhase.CutOff, update.Detail);
                }

                break;
            case LiveRoomStatusSignal.TitleChanged:
                ApplyRoomTitle(update.Title, update.AreaName);
                break;
            case LiveRoomStatusSignal.OnlineCountChanged:
                if (_roomStatus.ApplyOnlineCount(roomId, update.OnlineCount, update.OnlineCountSource))
                {
                    RefreshOnlineCount();
                }

                break;
            case LiveRoomStatusSignal.PlayUrlReload:
                if (_roomStatus.TryBeginReload(roomId, DateTimeOffset.Now))
                {
                    RequestControlledReload();
                }

                break;
            default:
                break;
        }
    }

    private void ApplyRoomTitle(string? title, string? areaName)
    {
        if (string.IsNullOrWhiteSpace(title) || string.Equals(title, Title, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(areaName))
            {
                TagName = areaName;
            }

            return;
        }

        Session.SetTitle(title);
        Title = title;
        if (!string.IsNullOrWhiteSpace(areaName))
        {
            TagName = areaName;
        }

        Parent.Title = title;
        if (Parent.Window?.GetWindow() is { } window)
        {
            window.Title = title;
        }

        // 系统媒体控制卡：标题/副标题/封面一起刷新。
        UpdateProperties();
    }

    private void ApplyTerminalState(LiveRoomPhase phase, string? detail)
    {
        IsRoomTerminal = true;
        RoomStateTitle = ResourceToolkit.GetLocalizedString(
            phase == LiveRoomPhase.CutOff ? StringNames.LiveRoomCutOffTitle : StringNames.LiveRoomNotStartedTitle);
        RoomStateDescription = string.IsNullOrWhiteSpace(detail)
            ? ResourceToolkit.GetLocalizedString(
                phase == LiveRoomPhase.CutOff ? StringNames.LiveRoomCutOffDescription : StringNames.LiveRoomNotStartedDescription)
            : detail;
        RoomStatusText = RoomStateTitle;
        IsLiving = false;
        _uptimeTimer?.Stop();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("直播间 {RoomId} 进入终态：{Phase}", _roomStatus.RoomId, phase);
        }
        Parent.ApplyLiveRoomTerminal(phase, RoomStateDescription);
    }

    private void ClearTerminalState()
    {
        IsRoomTerminal = false;
        RoomStateTitle = default;
        RoomStateDescription = default;
        IsLiving = true;
        RoomStatusText = ResourceToolkit.GetLocalizedString(StringNames.LiveRoomLiving);
        StartUptimeTimer();
        Parent.ClearLiveRoomTerminal();
    }

    private void RequestControlledReload()
    {
        if (IsRoomTerminal)
        {
            return;
        }

        _ = ObserveControlledReloadAsync();
    }

    private async Task ObserveControlledReloadAsync()
    {
        try
        {
            // 与手动刷新、画质/线路切换共用播放器的单飞准入。
            await Parent.RequestLivePlayUrlReloadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "直播间 {RoomId} 重载播放地址失败", _roomStatus.RoomId);
        }
    }

    private void SeedOnlineCount()
    {
        var viewerCount = Session.Page?.Information.GetExtensionIfNotNull<double>(LiveExtensionDataId.ViewerCount);
        if (viewerCount is { } count && count >= 0)
        {
            _roomStatus.ApplyOnlineCount(_roomStatus.RoomId, (int)count, LiveOnlineCountSource.Popularity);
        }

        RefreshOnlineCount();
    }

    private void RefreshOnlineCount()
        => OnlineCountText = _roomStatus.OnlineCount is { } count ? AppToolkit.FormatCount(count) : default;

    private void StartUptimeTimer()
    {
        if (_uptimeTimer is null)
        {
            _uptimeTimer = this.Get<DispatcherQueue>().CreateTimer();
            _uptimeTimer.Interval = TimeSpan.FromSeconds(30);
            _uptimeTimer.Tick += OnUptimeTimerTick;
        }

        _uptimeTimer.Start();
        RefreshUptime();
    }

    private void OnUptimeTimerTick(DispatcherQueueTimer sender, object args) => RefreshUptime();

    private void RefreshUptime()
    {
        if (IsRoomTerminal)
        {
            return;
        }

        var startTime = Session.Page?.Information.GetExtensionIfNotNull<DateTimeOffset?>(LiveExtensionDataId.StartTime);
        if (startTime is null || !Session.IsLiving)
        {
            UptimeText = default;
            return;
        }

        var elapsed = DateTimeOffset.Now - startTime.Value;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        UptimeText = LiveRoomStatusText.FormatDuration(
            elapsed,
            ResourceToolkit.GetLocalizedString(StringNames.LiveUptimeMinutesTemplate),
            ResourceToolkit.GetLocalizedString(StringNames.LiveUptimeHoursTemplate),
            ResourceToolkit.GetLocalizedString(StringNames.LiveUptimeHoursMinutesTemplate));
    }
}
