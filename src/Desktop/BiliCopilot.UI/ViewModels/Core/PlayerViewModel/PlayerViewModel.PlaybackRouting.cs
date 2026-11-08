// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.PlaybackRouting;
using BiliCopilot.UI.Resolvers;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.MpvKernel;
using Richasy.MpvKernel.Player.Models;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    private readonly PlaybackRefreshController _playbackRefresh = new();
    private Task _nativeRefreshDrained = Task.CompletedTask;
    private bool _isPlaybackRefreshReload;

    /// <summary>Gets whether this media supports an explicit playurl refresh.</summary>
    public bool IsPlaybackRefreshAvailable => _sourceResolver is VideoMediaSourceResolver or PgcMediaSourceResolver { IsDashPlayback: true } or LiveMediaSourceResolver;

    private bool CanRefreshPlayback()
        => IsPlaybackRefreshAvailable && IsSessionCurrent()
            && !IsConnecting && !_playbackRefresh.IsRefreshing
            && (IsLive || Player?.IsPlaybackInitialized == true);

    [RelayCommand(CanExecute = nameof(CanRefreshPlayback))]
    private async Task RefreshPlaybackAsync()
    {
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
            (source, token) => ReopenPlaybackAsync(source, token, isCurrent),
            () => isCurrent() && ReferenceEquals(resolver, _sourceResolver),
            ReportPlaybackRefreshFailure);

    /// <summary>
    /// 直播刷新入口：手动刷新与错误自动恢复共用同一次准入（同一时刻只允许一次），
    /// 只有取址方式不同——手动一定重新请求 playurl，自动恢复先换候选。
    /// 关闭/换房由 <paramref name="isCurrent"/> 与代际判断拦住迟到的结果。
    /// </summary>
    private async Task RefreshLivePlaybackAsync(LiveMediaSourceResolver resolver, bool automatic, Func<bool> isCurrent)
    {
        try
        {
            Func<CancellationToken, Task<MpvMediaSource>> fetch = automatic
                ? resolver.GetRecoverySourceAsync
                : resolver.RefreshSourceAsync;
            await _playbackRefresh.RefreshAsync(
                fetch,
                (source, token) => ReopenLivePlaybackAsync(source, token, isCurrent),
                () => isCurrent() && ReferenceEquals(resolver, _sourceResolver),
                ReportPlaybackRefreshFailure);
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

    private async Task ReopenPlaybackAsync(MpvMediaSource source, CancellationToken cancellationToken, Func<bool> isCurrent)
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
            if (!isCurrent())
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

            source.Options.StartPosition = position.Value;
            source.Options.InitialSpeed = speed.Value;
            source.Options.InitialVolume = volume.Value;
            // loadfile preserves the client's pause/mute flags. ReplayAsync does not:
            // it unconditionally resumes, so use the same client's explicit load here.
            _isPlaybackRefreshReload = true;
            _continuePosition = null;
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
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _positionChangesDrained = completion.Task;
        _nativeRefreshDrained = completion.Task;
        try
        {
            await previousPosition;
            cancellationToken.ThrowIfCancellationRequested();
            if (!isCurrent())
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
            for (var attempt = 1; ; attempt++)
            {
                var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnLoaded(object? sender, EventArgs args) => loaded.TrySetResult();
                void OnError(object? sender, MpvError error) => loaded.TrySetException(new InvalidOperationException($"直播刷新失败：{error}"));
                client.ReachFileLoaded += OnLoaded;
                client.ErrorOccurred += OnError;
                try
                {
                    await client.PlayAsync(source.Url, source.Options);
                    await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
                    break;
                }
                catch (InvalidOperationException ex) when (attempt < 3 && ex.Message.Contains("VoInitFailed", StringComparison.Ordinal))
                {
                    // 快速重开时旧 VO 尚未释放，mpv 偶发 VoInitFailed；短暂等待后重试，不把它当终态。
                    logger.LogWarning("直播视频输出初始化失败，第 {Attempt} 次重试", attempt);
                    await Task.Delay(400 * attempt, cancellationToken);
                }
                finally
                {
                    client.ReachFileLoaded -= OnLoaded;
                    client.ErrorOccurred -= OnError;
                }
            }
        }
        finally
        {
            completion.SetResult();
        }
    }

    internal async Task<bool> PreparePlaybackIntentAsync()
    {
        var isCurrent = CapturePlaybackValidity();
        CancelPlaybackRefresh();
        await WaitForNativeRefreshAsync();
        return isCurrent();
    }

    /// <summary>
    /// 切换直播清晰度：只持久化清晰度标识，然后重新取址并原位重开。
    /// </summary>
    internal Task ChangeLiveQualityAsync(int quality)
    {
        if (_sourceResolver is not LiveMediaSourceResolver resolver || !IsSessionCurrent())
        {
            return Task.CompletedTask;
        }

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