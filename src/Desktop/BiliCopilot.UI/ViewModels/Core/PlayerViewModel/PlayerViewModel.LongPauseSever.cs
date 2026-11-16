// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Player;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.MpvKernel.Core.Enums;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    private LongPauseSeverMonitor? _longPauseSeverMonitor;
    private DispatcherQueueTimer? _longPauseSeverTimer;

    /// <summary>
    /// #227 的恢复决策入口：当前会话是否因长暂停主动断流而等待恢复。
    /// </summary>
    internal bool IsSeveredByLongPause => _longPauseSeverMonitor?.IsSeveredByLongPause == true;

    /// <summary>
    /// #227 的恢复决策入口：长暂停断流时已记录的待恢复位置（loadfile 的 StartPosition）。
    /// </summary>
    internal double? LongPausePendingResumePosition => _longPauseSeverMonitor?.PendingResumePosition;

    private LongPauseSeverMonitor LongPauseSever
        => _longPauseSeverMonitor ??= new LongPauseSeverMonitor(
            LongPauseSeverPolicy.Threshold,
            ScheduleLongPauseSever,
            ExecuteLongPauseSever);

    private ILongPauseSeverTimer ScheduleLongPauseSever(TimeSpan delay)
    {
        var timer = queue.CreateTimer();
        timer.Interval = delay;
        timer.Tick += OnLongPauseSeverTick;
        timer.Start();
        _longPauseSeverTimer = timer;
        return new LongPauseSeverTimerHandle(this, timer);
    }

    private void OnLongPauseSeverTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        sender.Tick -= OnLongPauseSeverTick;
        if (ReferenceEquals(sender, _longPauseSeverTimer))
        {
            _longPauseSeverTimer = null;
        }

        if (!IsSessionCurrent() || IsLive || IsLiveRoomTerminal || Player is null
            || Player.PlaybackState != MpvPlayerState.Paused)
        {
            _longPauseSeverMonitor?.CancelTimer();
            return;
        }

        LongPauseSever.Sever(Player.Position);
    }

    private void ExecuteLongPauseSever(double position)
    {
        if (Client?.IsDisposed != false || IsPlaybackCommandBlocked())
        {
            return;
        }

        var media = _snapshot is null ? "unknown" : DescribeSnapshot(_snapshot);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "长暂停超过阈值（{Threshold}）主动断流：{Media} 位置 {Position:F1}s",
                LongPauseSeverPolicy.Threshold,
                media,
                position);
        }

        _ = Client.StopAsync();
    }

    private void HandleLongPauseStateChange()
        => LongPauseSever.OnPlaybackStateChanged(Player!.PlaybackState, IsLive || IsLiveRoomTerminal);

    /// <summary>
    /// 取消未决的长暂停断流计时；seek 等手动动作的取消点。
    /// </summary>
    partial void CancelLongPauseSever()
        => _longPauseSeverMonitor?.CancelTimer();

    /// <summary>
    /// loadfile 恢复期间抑制断流计时（恢复的 paused 不是用户长暂停）。
    /// </summary>
    partial void SuppressLongPauseSever()
        => _longPauseSeverMonitor?.Suppress();

    /// <summary>
    /// 解除 <see cref="SuppressLongPauseSever"/>；恢复结束（成功或失败）后调用。
    /// </summary>
    partial void UnsuppressLongPauseSever()
        => _longPauseSeverMonitor?.Unsuppress();

    /// <summary>
    /// 关闭/换集/初始化时重置长暂停断流状态（含待恢复位置与断流标记）。
    /// </summary>
    partial void ResetLongPauseSever()
    {
        _longPauseSeverMonitor?.Reset();
        if (_longPauseSeverTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnLongPauseSeverTick;
            _longPauseSeverTimer = null;
        }
    }

    /// <summary>
    /// #227 消费后调用：清除待恢复位置与断流标记（恢复成功或主动放弃恢复）。
    /// </summary>
    internal void ClearLongPausePendingResume()
        => _longPauseSeverMonitor?.ClearPendingResume();

    /// <summary>
    /// 让 DispatcherQueueTimer 满足可取消句柄契约；取消即停止并解绑回调。
    /// </summary>
    private sealed class LongPauseSeverTimerHandle(PlayerViewModel owner, DispatcherQueueTimer timer) : ILongPauseSeverTimer
    {
        public void Cancel()
        {
            timer.Stop();
            timer.Tick -= owner.OnLongPauseSeverTick;
        }
    }
}
