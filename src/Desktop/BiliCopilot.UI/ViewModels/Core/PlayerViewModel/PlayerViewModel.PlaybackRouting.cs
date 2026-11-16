// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Player;
using BiliCopilot.UI.PlaybackRouting;
using BiliCopilot.UI.Resolvers;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.MpvKernel;
using Richasy.MpvKernel.Core.Enums;
using Richasy.MpvKernel.Player.Models;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    /// <summary>
    /// 播放核心生死闸门：<c>MpvClient.Shutdown</c> 触发后置 dead，
    /// 之后所有发往 <see cref="Client"/>/<see cref="Player"/> 的命令在入口短路。
    /// #228 的重建路径在换成新客户端后调用 <see cref="PlaybackCoreGate.Reset"/>。
    /// </summary>
    private readonly PlaybackCoreGate _playbackCoreGate = new();
    private readonly LivePlaybackWatchdog _liveWatchdog = new();
    private int _liveNativeAttempt;
    private bool _resumeAfterLiveTerminal;
    private Task _liveTerminalPauseDrained = Task.CompletedTask;
    private readonly PlaybackRefreshController _playbackRefresh = new();
    private Task _nativeRefreshDrained = Task.CompletedTask;
    private bool _isPlaybackRefreshReload;

    /// <summary>
    /// 死核心重建编排（#228）：固定"取消未决 → 释放 player → 释放 client → 重新初始化"的顺序，
    /// 单飞汇入；释放路径通过 <see cref="PlaybackCoreRebuildCoordinator.Drained"/> 与重建汇合。
    /// 实现见 PlayerViewModel.CoreRebuild.cs。
    /// </summary>
    private readonly PlaybackCoreRebuildCoordinator _coreRebuild = new();

    /// <summary>Gets whether this media supports an explicit playurl refresh.</summary>
    public bool IsPlaybackRefreshAvailable => _sourceResolver is VideoMediaSourceResolver or PgcMediaSourceResolver { IsDashPlayback: true } or LiveMediaSourceResolver;

    private bool CanRefreshPlayback()
        => IsSessionCurrent()
            && !IsConnecting && !_playbackRefresh.IsRefreshing
            && (_playbackCoreGate.IsCoreDead
                || (IsPlaybackRefreshAvailable
                    && !IsPlaybackCommandBlocked()
                    && (IsLive || Player?.IsPlaybackInitialized == true)));

    [RelayCommand(CanExecute = nameof(CanRefreshPlayback))]
    private async Task RefreshPlaybackAsync()
    {
        // 核心已死：刷新按钮就是"重新加载"入口，走完整重建而不是取址刷新。
        if (_playbackCoreGate.IsCoreDead)
        {
            await RebuildPlaybackCoreAsync();
            return;
        }

        if (!CanRefreshPlayback())
        {
            return;
        }

        var resolver = _sourceResolver!;
        var isCurrent = CapturePlaybackValidity();
        var operation = resolver is LiveMediaSourceResolver liveResolver
            ? RefreshLivePlaybackAsync(liveResolver, automatic: false, isCurrent)
            : RefreshRecordedPlaybackAsync(resolver, isCurrent);
        RefreshPlaybackCommand.NotifyCanExecuteChanged();
        try
        {
            await operation;
        }
        finally
        {
            RefreshPlaybackCommand.NotifyCanExecuteChanged();
        }
    }

    private Task RefreshRecordedPlaybackAsync(MediaSourceResolverBase resolver, Func<bool> isCurrent)
        => _playbackRefresh.RefreshAsync(
            resolver.RefreshSourceAsync,
            (source, token) => ReopenPlaybackAsync(source, token, isCurrent, explicitStartPosition: null),
            () => isCurrent() && ReferenceEquals(resolver, _sourceResolver),
            ReportPlaybackRefreshFailure);

    /// <summary>
    /// #227：长暂停断流后用户点继续。Idle + 待恢复位置时不 resume，而是复用刷新通道重新取址，
    /// loadfile 回填断点位置继续，保留 pause/mute/倍速/音量。
    /// </summary>
    internal async Task<bool> ResumeFromLongPauseAsync()
    {
        if (_sourceResolver is not MediaSourceResolverBase resolver || _playbackRefresh.IsRefreshing)
        {
            return false;
        }

        var media = _snapshot is null ? "unknown" : DescribeSnapshot(_snapshot);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "长暂停断流后通过 loadfile 恢复播放：{Media} 位置 {Position:F1}s",
                media,
                LongPausePendingResumePosition ?? 0);
        }

        var isCurrent = CapturePlaybackValidity();
        var resumed = false;
        await _playbackRefresh.RefreshAsync(
            resolver.RefreshSourceAsync,
            async (source, token) =>
            {
                // 成功接纳 loadfile 后清除待恢复位置；loadfile 失败则保留以便重试。
                await ReopenPlaybackAsync(source, token, isCurrent, LongPausePendingResumePosition);
                resumed = true;
            },
            () => isCurrent() && ReferenceEquals(resolver, _sourceResolver),
            ReportPlaybackRefreshFailure);
        if (resumed)
        {
            ClearLongPausePendingResume();
        }

        return resumed;
    }

    /// <summary>
    /// 直播刷新入口：手动刷新与错误自动恢复共用同一次准入（同一时刻只允许一次），
    /// 只有取址方式不同——手动一定重新请求 playurl，自动恢复先换候选。
    /// 关闭/换房由 <paramref name="isCurrent"/> 与代际判断拦住迟到的结果。
    /// </summary>
    private async Task RefreshLivePlaybackAsync(LiveMediaSourceResolver resolver, bool automatic, Func<bool> isCurrent)
    {
        if (!automatic)
        {
            _liveWatchdog.ResetFailures();
        }

        try
        {
            Func<CancellationToken, Task<MpvMediaSource>> fetch = automatic
                ? resolver.GetRecoverySourceAsync
                : resolver.RefreshSourceAsync;
            var failureRecorded = false;
            async Task<MpvMediaSource> FetchAsync(CancellationToken token)
            {
                failureRecorded = false;
                return await fetch(token);
            }

            async Task<MpvMediaSource> FetchRecoveryAsync(CancellationToken token)
            {
                failureRecorded = false;
                return await resolver.GetRecoverySourceAsync(token);
            }

            await _playbackRefresh.RefreshAsync(
                FetchAsync,
                (source, token) => ReopenLivePlaybackAsync(source, token, isCurrent),
                () => isCurrent() && !IsLiveRoomTerminal && ReferenceEquals(resolver, _sourceResolver),
                exception =>
                {
                    if (!failureRecorded)
                    {
                        _liveWatchdog.RecordFailure();
                    }

                    ReportPlaybackRefreshFailure(exception);
                },
                retry: exception =>
                {
                    failureRecorded = true;
                    var canRetry = _liveWatchdog.RecordFailure();
                    var kind = exception is LivePlaybackUnavailableException unavailable
                        ? unavailable.Kind
                        : LivePlaybackFailureClassifier.ClassifyPlayback(exception);
                    return canRetry && resolver.NotifyPlaybackFailure(kind) is LiveRecoveryAction.AdvanceCandidate or LiveRecoveryAction.ReRequestPlayUrl;
                },
                retryFetch: FetchRecoveryAsync);
        }
        finally
        {
            if (IsSessionCurrent() && ReferenceEquals(resolver, _sourceResolver) && Connector is LiveConnectorViewModel liveConnector)
            {
                liveConnector.ApplyPlaybackInfo(resolver);
            }

            RefreshPlaybackCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task ReopenPlaybackAsync(MpvMediaSource source, CancellationToken cancellationToken, Func<bool> isCurrent, double? explicitStartPosition)
    {
        var previousPosition = _positionChangesDrained;
        var previousSpeed = WaitForSpeedChangesAsync();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Join the existing seek/release queue before yielding. Later seeks run after loadfile.
        _positionChangesDrained = completion.Task;
        _nativeRefreshDrained = completion.Task;
        try
        {
#pragma warning disable VSTHRD003 // UI-owned queues; no synchronous dispatcher wait.
            await previousPosition;
            await previousSpeed;
#pragma warning restore VSTHRD003
            cancellationToken.ThrowIfCancellationRequested();
            if (!isCurrent() || IsPlaybackCommandBlocked())
            {
                return;
            }

            var client = Client!;
            var position = await client.GetCurrentPositionAsync();
            var speed = await client.GetSpeedAsync();
            var volume = await client.GetVolumeAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (!isCurrent())
            {
                return;
            }

            if (!position.IsSuccess || !speed.IsSuccess || !volume.IsSuccess)
            {
                throw new InvalidOperationException("无法读取当前播放状态，请稍后重试。");
            }

            source.Options.StartPosition = explicitStartPosition ?? position.Value;
            source.Options.InitialSpeed = speed.Value;
            source.Options.InitialVolume = volume.Value;
            // loadfile preserves the client's pause/mute flags. ReplayAsync does not:
            // it unconditionally resumes, so use the same client's explicit load here.
            _isPlaybackRefreshReload = true;
            _continuePosition = null;
            _liveNativeAttempt++;
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnLoaded(object? sender, EventArgs args) => loaded.TrySetResult();
            void OnError(object? sender, MpvError error) => loaded.TrySetException(new InvalidOperationException($"刷新播放失败：{error}"));
            client.ReachFileLoaded += OnLoaded;
            client.ErrorOccurred += OnError;
            try
            {
                await client.PlayAsync(source.Url, source.Options);
                // Do not abandon an admitted native load on a newer user intent.
                // Those operations and teardown join this queue; failed loading is bounded.
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            finally
            {
                client.ReachFileLoaded -= OnLoaded;
                client.ErrorOccurred -= OnError;
            }
        }
        finally
        {
            completion.SetResult();
        }
    }

    /// <summary>
    /// 直播重开：只把音量带过去，不恢复位置与倍速，也不继承签名地址。
    /// 直播没有时间轴，恢复位置会让新签名地址从旧位置起播。
    /// </summary>
    private async Task ReopenLivePlaybackAsync(MpvMediaSource source, CancellationToken cancellationToken, Func<bool> isCurrent)
    {
        // Capture the queue before replacing it: the helper owns the (already suppressed)
        // await of this instance's UI-owned seek queue.
        var previousPosition = WaitForPositionChangesAsync();
        var previousTerminalPause = _liveTerminalPauseDrained;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _positionChangesDrained = completion.Task;
        _nativeRefreshDrained = completion.Task;
        try
        {
            await previousPosition;
#pragma warning disable VSTHRD003 // Terminal pause belongs to this UI-owned playback queue.
            await previousTerminalPause;
#pragma warning restore VSTHRD003
            cancellationToken.ThrowIfCancellationRequested();
            if (!isCurrent() || IsPlaybackCommandBlocked())
            {
                return;
            }

            var client = Client!;
            var volume = await client.GetVolumeAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (!isCurrent())
            {
                return;
            }

            source.Options.StartPosition = null;
            source.Options.InitialSpeed = 1d;
            if (volume.IsSuccess)
            {
                source.Options.InitialVolume = volume.Value;
            }

            _isPlaybackRefreshReload = true;
            _continuePosition = null;
            _liveNativeAttempt++;
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnLoaded(object? sender, EventArgs args) => loaded.TrySetResult();
            void OnError(object? sender, MpvError error) => loaded.TrySetException(
                new LivePlaybackUnavailableException(LiveNativePlaybackFailure.Classify(error), $"直播刷新失败：{error}"));
            client.ReachFileLoaded += OnLoaded;
            client.ErrorOccurred += OnError;
            try
            {
                await client.PlayAsync(source.Url, source.Options);
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
                if (!cancellationToken.IsCancellationRequested && isCurrent() && !IsLiveRoomTerminal)
                {
                    if (_resumeAfterLiveTerminal)
                    {
                        await client.ResumeAsync();
                        _resumeAfterLiveTerminal = false;
                    }

                    _liveWatchdog.RecordSuccess();
                }
            }
            finally
            {
                client.ReachFileLoaded -= OnLoaded;
                client.ErrorOccurred -= OnError;
            }
        }
        finally
        {
            completion.SetResult();
        }
    }

    internal void RememberLiveTerminalPause()
        => _resumeAfterLiveTerminal = Player?.PlaybackState != MpvPlayerState.Paused;

    internal async Task<bool> PreparePlaybackIntentAsync(bool changesPause = false)
    {
        if (IsPlaybackCommandBlocked())
        {
            return false;
        }

        var isCurrent = CapturePlaybackValidity();
        if (changesPause)
        {
            _resumeAfterLiveTerminal = false;
        }

        CancelPlaybackRefresh();
        await WaitForNativeRefreshAsync();
        return isCurrent() && !IsPlaybackCommandBlocked();
    }

    /// <summary>
    /// 播放核心已退出（Shutdown）或客户端已释放时，一切命令必须短路，
    /// 不再下发到已退出的 mpv 句柄。
    /// </summary>
    private bool IsPlaybackCommandBlocked() => _playbackCoreGate.ShouldShortCircuit(Client?.IsDisposed);

    /// <summary>
    /// 死核心重建：先完整释放旧 <c>MpvPlayer</c>+<c>MpvClient</c>，再走
    /// <c>InitializeAsync</c> 的 <c>Client?.IsDisposed != false</c> 分支重建（#228）。
    /// 实现见 PlayerViewModel.CoreRebuild.cs（依赖真实 MpvPlayer/MpvClient，测试工程用替身实现）。
    /// </summary>
    private partial Task RebuildPlaybackCoreAsync();

    /// <summary>
    /// 切换直播清晰度：只持久化清晰度标识，然后重新取址并原位重开。
    /// </summary>
    internal Task ChangeLiveQualityAsync(int quality)
    {
        if (_sourceResolver is not LiveMediaSourceResolver resolver || !IsSessionCurrent())
        {
            return Task.CompletedTask;
        }

        _liveWatchdog.ResetFailures();
        resolver.SelectQuality(quality);
        return RefreshLiveAsyncAfterPreferenceChangeAsync(resolver);
    }

    /// <summary>
    /// 切换直播线路：线路偏好只在内存里跨刷新保留，随后重新取址并原位重开。
    /// </summary>
    internal Task ChangeLiveLineAsync(string? key)
    {
        if (_sourceResolver is not LiveMediaSourceResolver resolver || !IsSessionCurrent())
        {
            return Task.CompletedTask;
        }

        _liveWatchdog.ResetFailures();
        resolver.SelectLine(key);
        return RefreshLiveAsyncAfterPreferenceChangeAsync(resolver);
    }

    private async Task RefreshLiveAsyncAfterPreferenceChangeAsync(LiveMediaSourceResolver resolver)
    {
        var isCurrent = CapturePlaybackValidity();
        try
        {
            await RefreshLivePlaybackAsync(resolver, automatic: false, isCurrent);
        }
        catch (Exception ex)
        {
            ReportPlaybackRefreshFailure(ex);
        }
    }

    private void CancelPlaybackRefresh() => _playbackRefresh.Cancel();

#pragma warning disable VSTHRD003 // Same UI-owned queue as WaitForPositionChangesAsync; asynchronously joined before native teardown.
    private Task WaitForNativeRefreshAsync() => _nativeRefreshDrained;
#pragma warning restore VSTHRD003

    partial void ReportPlaybackRefreshFailure(Exception exception);
}
