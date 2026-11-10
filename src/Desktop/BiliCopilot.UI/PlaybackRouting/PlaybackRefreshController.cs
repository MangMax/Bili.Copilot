// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.PlaybackRouting;

// Admission is owned by the UI thread, just like PlaybackSession and the seek queue.
// This controller owns an operation, never a second player/session lifecycle.
internal sealed class PlaybackRefreshController
{
    private CancellationTokenSource? _cancellation;
    private TaskCompletionSource? _completion;

    public Task Completion => _completion?.Task ?? Task.CompletedTask;

    public bool IsRefreshing => _cancellation is not null;

    public void Cancel() => _cancellation?.Cancel();

    public async Task RefreshAsync<T>(Func<CancellationToken, Task<T>> fetch, Func<T, CancellationToken, Task> reopen,
        Func<bool> isCurrent, Action<Exception> reportError, TimeSpan? timeout = null,
        Func<Exception, bool>? retry = null, Func<CancellationToken, Task<T>>? retryFetch = null)
    {
        if (IsRefreshing || !isCurrent())
        {
            return;
        }

        using var userCancellation = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(20));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(userCancellation.Token, deadline.Token);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;
        _cancellation = userCancellation;
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    var source = await fetch(cancellation.Token).WaitAsync(cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (isCurrent())
                    {
                        // Every admitted native load is joined before retry, cancellation or teardown.
                        await reopen(source, cancellation.Token);
                    }

                    return;
                }
                catch (Exception ex) when (!cancellation.IsCancellationRequested && isCurrent())
                {
                    if (retry is null || !retry(ex) || attempt >= 2)
                    {
                        throw;
                    }

                    fetch = retryFetch ?? fetch;
                }
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
            completion.SetResult();
        }
    }
}
