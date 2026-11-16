// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.MpvKernel.Core.Enums;

namespace BiliCopilot.UI.Player;

/// <summary>
/// 长暂停主动断流策略：阈值只有一个来源，#226 的实现与 #227 的恢复决策共用。
/// </summary>
internal static class LongPauseSeverPolicy
{
    /// <summary>
    /// 暂停多久后主动断流（默认落在 CDN 分片 TTL 量级）。
    /// </summary>
    internal static readonly TimeSpan Threshold = TimeSpan.FromMinutes(5);
}

/// <summary>
/// 长暂停计时器的可取消调度抽象；生产端由 DispatcherQueueTimer 实现，测试端用假时钟驱动。
/// </summary>
internal interface ILongPauseSeverTimer
{
    /// <summary>
    /// 取消当前计时并释放调度资源；重复调用安全。
    /// </summary>
    void Cancel();
}

/// <summary>
/// 长暂停断流状态机：进入 Paused 时启动一次计时；阈值到达仍暂停则执行断流动作；
/// 任何恢复/换集/关闭/任意播放状态变化都会取消计时。
/// 断流后记录待恢复位置与标记，供 #227 的 loadfile 恢复决策消费。
/// </summary>
internal sealed class LongPauseSeverMonitor
{
    private readonly Func<TimeSpan, ILongPauseSeverTimer> _schedule;
    private readonly Action<double> _sever;
    private readonly TimeSpan _threshold;
    private ILongPauseSeverTimer? _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LongPauseSeverMonitor"/> class.
    /// </summary>
    /// <param name="threshold">暂停多久算长暂停；通常取 <see cref="LongPauseSeverPolicy.Threshold"/>。</param>
    /// <param name="schedule">按给定延迟调度一次断流回调；返回的可取消句柄归监视器管理。</param>
    /// <param name="sever">断流动作：记录位置并调用 Client.StopAsync()；由视图模型提供。</param>
    public LongPauseSeverMonitor(TimeSpan threshold, Func<TimeSpan, ILongPauseSeverTimer> schedule, Action<double> sever)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(threshold, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(sever);
        _threshold = threshold;
        _schedule = schedule;
        _sever = sever;
    }

    /// <summary>
    /// 待恢复位置：断流时已记录的播放位置；#227 读取它作为 loadfile 的 StartPosition。
    /// </summary>
    internal double? PendingResumePosition { get; private set; }

    /// <summary>
    /// 待恢复标记：当前会话是否因长暂停主动断流而等待恢复（区别于自然 Idle/换集）。
    /// </summary>
    internal bool IsSeveredByLongPause { get; private set; }

    /// <summary>
    /// 当前是否有未决的长暂停计时（已进入 Paused 且尚未触发或取消）。
    /// </summary>
    internal bool IsPending => _timer is not null;

    /// <summary>
    /// 进入暂停：启动可取消计时；直播、非暂停进入或已有未决计时时不重复启动。
    /// </summary>
    /// <param name="isLive">直播豁免：为 true 时完全不参与长暂停断流。</param>
    private void OnPaused(bool isLive)
    {
        CancelTimer();
        if (isLive)
        {
            return;
        }

        var timer = _schedule(_threshold);
        _timer = timer ?? throw new InvalidOperationException("长暂停调度器必须返回可取消句柄");
    }

    /// <summary>
    /// 播放状态变化的统一入口：进入 Paused 启动计时，任何其他状态取消计时。
    /// 覆盖恢复/播放/seek/换集/关闭等所有非暂停变更。
    /// </summary>
    /// <param name="state">新的播放状态。</param>
    /// <param name="isLive">直播豁免。</param>
    internal void OnPlaybackStateChanged(MpvPlayerState state, bool isLive)
    {
        if (state == MpvPlayerState.Paused)
        {
            OnPaused(isLive);
            return;
        }

        CancelTimer();
    }

    /// <summary>
    /// 任意非暂停的播放状态变化、恢复、seek、换集、关闭或释放时的统一取消入口。
    /// 断流标记由 #227 的消费方调用 <see cref="ClearPendingResume"/> 清除，这里不重置。
    /// </summary>
    internal void CancelTimer()
    {
        _timer?.Cancel();
        _timer = null;
    }

    /// <summary>
    /// 计时器到点：若仍是暂停会话则执行断流（记录位置 + 标记 + 调用 sever）。
    /// 返回是否真正执行了断流；重复/迟到触发安全返回 false。
    /// </summary>
    internal bool Sever(double position)
    {
        if (_timer is null)
        {
            return false;
        }

        _timer = null;
        PendingResumePosition = position;
        IsSeveredByLongPause = true;
        _sever(position);
        return true;
    }

    /// <summary>
    /// 清除待恢复位置与断流标记；#227 在成功恢复或主动放弃恢复时调用。
    /// </summary>
    internal void ClearPendingResume()
    {
        PendingResumePosition = null;
        IsSeveredByLongPause = false;
    }

    /// <summary>
    /// 释放/关闭入口：取消计时并清空所有待恢复状态。
    /// </summary>
    internal void Reset()
    {
        CancelTimer();
        ClearPendingResume();
    }
}
