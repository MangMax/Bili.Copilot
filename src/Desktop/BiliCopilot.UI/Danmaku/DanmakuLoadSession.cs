// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Danmaku;

namespace BiliCopilot.UI.Danmaku;

internal sealed record DanmakuLoadState(bool IsLoading, IList<DanmakuInformation>? Items = null, Exception? Error = null);

/// <summary>
/// Owns one player's danmaku load, including requests and queued UI publications.
/// Replacing or closing a session invalidates publications before canceling transport work.
/// </summary>
internal sealed class DanmakuLoadSession(IDanmakuService service, Action<Action> dispatch, Action<DanmakuLoadState> publish) : IDisposable
{
    private readonly object _gate = new();
    private Session? _current;
    private bool _disposed;

    public Task LoadAsync(string aid, string cid, int segmentCount)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            Cancel();
            var session = new Session();
            _current = session;
            Publish(session, new(true));
            return LoadCoreAsync(session, aid, cid, segmentCount);
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            var previous = _current;
            _current = null;
            if (previous is { Completed: false })
            {
                previous.Source.Cancel();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            Cancel();
        }
    }

    private async Task LoadCoreAsync(Session session, string aid, string cid, int segmentCount)
    {
        using var semaphore = new SemaphoreSlim(6, 6);
        var token = session.Source.Token;
        var items = new List<DanmakuInformation>();
        try
        {
            await Task.WhenAll(Enumerable.Range(1, segmentCount).Select(LoadSegmentAsync)).ConfigureAwait(false);
            Publish(session, new(false, items));
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            // Cancellation can be wrapped by a transport; obsolete sessions are silent.
        }
        finally
        {
            lock (_gate)
            {
                session.Completed = true;
                session.Source.Dispose();
            }
        }

        async Task LoadSegmentAsync(int index)
        {
            // A canceled wait owns no permit. Only release after a successful wait.
            await semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                Task<IReadOnlyList<DanmakuInformation>> request;
                lock (_gate)
                {
                    token.ThrowIfCancellationRequested();
                    request = service.GetSegmentDanmakusWithGrpcAsync(aid, cid, index, token);
                }

                var result = await request.ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                lock (items)
                {
                    items.AddRange(result);
                }
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                // Keep the existing partial-results behavior on non-cancellation failures.
                Publish(session, new(true, Error: ex));
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    private void Publish(Session session, DanmakuLoadState state)
        => dispatch(() =>
        {
            // Check at dispatch execution, not enqueue time, and serialize with invalidation.
            lock (_gate)
            {
                if (ReferenceEquals(_current, session) && !_disposed)
                {
                    publish(state);
                }
            }
        });

    private sealed class Session
    {
        public CancellationTokenSource Source { get; } = new();

        public bool Completed { get; set; }
    }
}
