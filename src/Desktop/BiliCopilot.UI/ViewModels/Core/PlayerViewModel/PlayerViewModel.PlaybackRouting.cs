// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.PlaybackRouting;
using BiliCopilot.UI.Resolvers;
using CommunityToolkit.Mvvm.Input;
using Richasy.MpvKernel;
using Richasy.MpvKernel.Player.Models;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    private readonly PlaybackRefreshController _playbackRefresh = new();
    private Task _nativeRefreshDrained = Task.CompletedTask;
    private bool _isPlaybackRefreshReload;

    /// <summary>Gets whether this media supports an explicit playurl refresh.</summary>
    public bool IsPlaybackRefreshAvailable => _sourceResolver is VideoMediaSourceResolver or PgcMediaSourceResolver { IsDashPlayback: true };

    private bool CanRefreshPlayback() => IsPlaybackRefreshAvailable && IsSessionCurrent()
        && Player?.IsPlaybackInitialized == true && !IsConnecting && !_playbackRefresh.IsRefreshing;

    [RelayCommand(CanExecute = nameof(CanRefreshPlayback))]
    private async Task RefreshPlaybackAsync()
    {
        if (!CanRefreshPlayback())
        {
            return;
        }

        var resolver = _sourceResolver!;
        var isCurrent = CapturePlaybackValidity();
        var operation = _playbackRefresh.RefreshAsync(resolver.RefreshSourceAsync,
            (source, token) => ReopenPlaybackAsync(source, token, isCurrent),
            () => isCurrent() && ReferenceEquals(resolver, _sourceResolver), ReportPlaybackRefreshFailure);
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

    internal async Task<bool> PreparePlaybackIntentAsync()
    {
        var isCurrent = CapturePlaybackValidity();
        CancelPlaybackRefresh();
        await WaitForNativeRefreshAsync();
        return isCurrent();
    }

    private void CancelPlaybackRefresh() => _playbackRefresh.Cancel();

#pragma warning disable VSTHRD003 // Same UI-owned queue as WaitForPositionChangesAsync; asynchronously joined before native teardown.
    private Task WaitForNativeRefreshAsync() => _nativeRefreshDrained;
#pragma warning restore VSTHRD003

    partial void ReportPlaybackRefreshFailure(Exception exception);
}
