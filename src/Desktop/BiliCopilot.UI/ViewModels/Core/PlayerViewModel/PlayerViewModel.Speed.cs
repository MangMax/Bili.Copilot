// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    internal Func<bool> CapturePlaybackValidity()
    {
        var player = Player;
        var client = Client;
        var version = _danmakuPlaybackVersion;
        return () => player is not null && client is not null && !_isDanmakuClosed
            && version == _danmakuPlaybackVersion
            && ReferenceEquals(player, Player) && ReferenceEquals(client, Client);
    }

    private int _pendingSpeedChanges;
    private TaskCompletionSource? _speedChangesDrained;

    /// <summary>
    /// 在 UI 线程接纳调速；关闭拒绝新请求，并在销毁客户端前等待已接纳的请求结束。
    /// </summary>
    /// <returns>调速成功且仍属于当前媒体时为 true。</returns>
    public async Task<bool> ChangeSpeedAsync(double speed)
    {
        // Like CloseAsync/DisposeAsync, admission runs synchronously on the UI thread.
        // Capture the client before yielding; never send an old request to a replacement.
        if (_isDanmakuClosed || Client is null || Player is null)
        {
            return false;
        }

        var client = Client;
        var isCurrent = CapturePlaybackValidity();
        CancelPlaybackRefresh();
        await WaitForNativeRefreshAsync();
        if (!isCurrent())
        {
            return false;
        }

        if (_pendingSpeedChanges++ == 0)
        {
            _speedChangesDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        try
        {
            var result = await client.SetSpeedAsync(speed);
            return result.IsSuccess && isCurrent();
        }
        finally
        {
            // Faults propagate to the caller, but must not strand native teardown.
            if (--_pendingSpeedChanges == 0)
            {
                _speedChangesDrained!.SetResult();
            }
        }
    }

    private Task WaitForSpeedChangesAsync() => _speedChangesDrained?.Task ?? Task.CompletedTask;
}
