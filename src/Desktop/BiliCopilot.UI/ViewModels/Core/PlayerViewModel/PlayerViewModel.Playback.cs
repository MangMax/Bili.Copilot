// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.MpvKernel.Core.Enums;
using System.ComponentModel;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    private long _danmakuSeekVersion;
    private Task _positionChangesDrained = Task.CompletedTask;

    private void OnDanmakuPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isDanmakuClosed || !ReferenceEquals(sender, Player))
        {
            return;
        }

        if (e.PropertyName == nameof(Player.Position))
        {
            Danmaku.UpdatePosition(Convert.ToInt32(Player.Position));
        }
        else if (e.PropertyName == nameof(Player.PlaybackRate))
        {
            Danmaku.ExtraSpeed = Player.PlaybackRate;
        }
        else if (e.PropertyName == nameof(Player.PlaybackState))
        {
            if (Player.PlaybackState == MpvPlayerState.Playing)
            {
                Danmaku.Resume();
            }
            else
            {
                Danmaku.Pause();
            }
        }
    }

    /// <summary>
    /// 改变当前播放位置.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    public async Task ChangePositionAsync(double position)
    {
        if (Player is null || Client is null || _isDanmakuClosed
            || !double.IsFinite(position) || position < 0 || (Player.Duration > 0 && position > Player.Duration))
        {
            return;
        }

        CancelPlaybackRefresh();
        var player = Player;
        var client = Client;
        var isCurrent = CapturePlaybackValidity();
        var seekVersion = ++_danmakuSeekVersion;
        var previous = _positionChangesDrained;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _positionChangesDrained = completion.Task;
#pragma warning disable VSTHRD003 // UI-owned asynchronous queue; never blocks the dispatcher.
        await previous;
#pragma warning restore VSTHRD003
        try
        {
            // Admission and continuations run on the UI thread. Obsolete queued
            // requests must never write to the old or a replacement client.
            if (!isCurrent() || seekVersion != _danmakuSeekVersion)
            {
                return;
            }

            var succeeded = true;
            if (player.PlaybackState == MpvPlayerState.End)
            {
                await player.ReplayAsync(position);
            }
            else
            {
                // MpvClient awaits synchronous mpv_set_property(time-pos).
                // This orders absolute-seek submissions, not rendered frames:
                // mpv may coalesce pending seeks; the final submission must win.
                succeeded = (await client.SetCurrentPositionAsync(position)).IsSuccess;
            }

            if (succeeded && isCurrent() && seekVersion == _danmakuSeekVersion)
            {
                Danmaku.Seek(Convert.ToInt32(position));
            }
        }
        finally
        {
            // Propagate faults while allowing the latest queued request to run.
            completion.SetResult();
        }
    }

#pragma warning disable VSTHRD003 // Close/media replacement asynchronously join this instance's UI-owned queue.
    private Task WaitForPositionChangesAsync() => _positionChangesDrained;
#pragma warning restore VSTHRD003
}
