// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.PlaybackRouting;

// Admission is owned by the UI thread, just like PlaybackSession and the seek queue.
// This controller owns an operation, never a second player/session lifecycle.
internal sealed class PlaybackRefreshController
{
    private CancellationTokenSource? _cancellation;

    public bool IsRefreshing => _cancellation is not null;

    public void Cancel() => _cancellation?.Cancel();

    public async Task RefreshAsync<T>(Func<CancellationToken, Task<T>> fetch, Func<T, CancellationToken, Task> reopen,
        Func<bool> isCurrent, Action<Exception> reportError, TimeSpan? timeout = null)
    {
        if (IsRefreshing || !isCurrent())
        {
            return;
        }

        using var userCancellation = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(20));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(userCancellation.Token, deadline.Token);
        _cancellation = userCancellation;
        try
        {
            var source = await fetch(cancellation.Token).WaitAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (isCurrent())
            {
                // Reopen must join any native write already admitted, even when cancelled.
                await reopen(source, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!userCancellation.IsCancellationRequested && isCurrent())
            {
                reportError(new TimeoutException("刷新播放超时，请稍后重试。"));
            }
        }
        catch (Exception ex)
        {
            if (isCurrent() && !userCancellation.IsCancellationRequested)
            {
                reportError(ex);
            }
        }
        finally
        {
            _cancellation = null;
        }
    }
}
