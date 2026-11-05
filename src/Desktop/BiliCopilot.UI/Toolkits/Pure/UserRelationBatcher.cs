// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.User;
using System.Globalization;

namespace BiliCopilot.UI.Toolkits;

/// <summary>Coalesces loaded-card relationship reads with account isolation and failure preservation.</summary>
public sealed class UserRelationBatcher : IDisposable
{
    private sealed record Entry(bool Following, DateTimeOffset Loaded);
    private readonly object _gate = new();
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, UserRelationStatus>>> _fetch;
    private readonly TimeProvider _clock;
    private readonly Action<Exception>? _onError;
    private readonly Dictionary<string, Entry> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource<bool?>> _pending = new(StringComparer.Ordinal);
    private readonly HashSet<string> _waiting = new(StringComparer.Ordinal);
    private CancellationTokenSource _scope = new();
    private string? _account;
    private int _generation;
    private bool _scheduled;
    private bool _disposed;
    private DateTimeOffset _retryAfter;

    /// <summary>Creates a shared batcher. A batch fetch must distinguish unknown from explicit unfollow.</summary>
    public UserRelationBatcher(
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, UserRelationStatus>>> fetch,
        TimeProvider? clock = null, Action<Exception>? onError = null)
    {
        _fetch = fetch;
        _clock = clock ?? TimeProvider.System;
        _onError = onError;
    }

    /// <summary>Drops the old account's cache, consumers and late responses.</summary>
    public void SetAccount(string? account)
    {
        CancellationTokenSource? previous;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_account == account) return;
            previous = _scope;
            _scope = new();
            _account = account;
            _generation++;
            _cache.Clear();
            foreach (var pending in _pending.Values) pending.TrySetResult(null);
            _pending.Clear();
            _waiting.Clear();
            _scheduled = false;
            _retryAfter = default;
        }
        previous.Cancel();
        previous.Dispose();
    }

    /// <summary>Records a relationship explicitly supplied by the current account's feed.</summary>
    public void SetKnownFollowing(string account, string userId, bool following)
    {
        lock (_gate)
        {
            if (_disposed || _account != account) return;
            Cache(userId, following);
        }
    }

    /// <summary>Returns last known state without treating stale or failed data as unfollowed.</summary>
    public bool? GetKnownFollowing(string? account, string userId)
    {
        lock (_gate) return !_disposed && _account == account && _cache.TryGetValue(userId, out var entry) ? entry.Following : null;
    }

    /// <summary>Queues a distinct author; canceling one caller does not cancel other callers' shared request.</summary>
    public Task<bool?> QueryAsync(string? account, string userId, CancellationToken cancellationToken = default)
    {
        SetAccount(account);
        if (account is null || userId == account
            || !long.TryParse(userId, NumberStyles.None, CultureInfo.InvariantCulture, out var mid) || mid <= 0
            || mid.ToString(CultureInfo.InvariantCulture) != userId)
            return Task.FromResult<bool?>(null);
        cancellationToken.ThrowIfCancellationRequested();
        Task<bool?> task;
        lock (_gate)
        {
            if (_disposed || _account != account) return Task.FromResult<bool?>(null);
            var now = _clock.GetUtcNow();
            _cache.TryGetValue(userId, out var cached);
            if (cached is not null && now - cached.Loaded < TimeSpan.FromMinutes(5))
                return Task.FromResult<bool?>(cached.Following);
            if (now < _retryAfter) return Task.FromResult<bool?>(cached?.Following);
            if (!_pending.TryGetValue(userId, out var pending))
            {
                pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending.Add(userId, pending);
                _waiting.Add(userId);
            }
            task = pending.Task;
            Schedule();
        }
        return task.WaitAsync(cancellationToken);
    }

    private void Cache(string userId, bool following)
    {
        if (_cache.Count >= 256 && !_cache.ContainsKey(userId))
            _cache.Remove(_cache.MinBy(item => item.Value.Loaded).Key);
        _cache[userId] = new(following, _clock.GetUtcNow());
    }

    private void Schedule()
    {
        if (_scheduled) return;
        _scheduled = true;
        _ = DrainAsync(_generation, _scope.Token);
    }

    private async Task DrainAsync(int generation, CancellationToken token)
    {
        try
        {
            await Task.Delay(50, token).ConfigureAwait(false);
            while (true)
            {
                string[] ids;
                Dictionary<string, Entry?> previous;
                lock (_gate)
                {
                    if (_disposed || generation != _generation || _waiting.Count == 0) return;
                    if (_clock.GetUtcNow() < _retryAfter)
                    {
                        foreach (var id in _waiting)
                            if (_pending.Remove(id, out var queued)) queued.TrySetResult(_cache.GetValueOrDefault(id)?.Following);
                        _waiting.Clear();
                        return;
                    }
                    ids = _waiting.Take(40).ToArray();
                    previous = ids.ToDictionary(id => id, id => _cache.GetValueOrDefault(id));
                    foreach (var id in ids) _waiting.Remove(id);
                }
                IReadOnlyDictionary<string, UserRelationStatus>? result = null;
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                    deadline.CancelAfter(TimeSpan.FromSeconds(20));
                    result = await _fetch(ids, deadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    lock (_gate)
                        if (generation == _generation) _retryAfter = _clock.GetUtcNow().AddSeconds(30);
                    _onError?.Invoke(ex);
                }
                lock (_gate)
                {
                    if (_disposed || generation != _generation) return;
                    foreach (var id in ids)
                    {
                        var current = _cache.GetValueOrDefault(id);
                        if (ReferenceEquals(current, previous[id]) && result is not null && result.TryGetValue(id, out var relation))
                        {
                            bool? following = relation switch
                            {
                                UserRelationStatus.Following or UserRelationStatus.Friends or UserRelationStatus.SpeciallyFollowed => true,
                                UserRelationStatus.Unfollow or UserRelationStatus.Blocked => false,
                                _ => null,
                            };
                            if (following.HasValue) Cache(id, following.Value);
                        }
                        if (_pending.Remove(id, out var pending)) pending.TrySetResult(_cache.GetValueOrDefault(id)?.Following);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            lock (_gate)
            {
                if (generation == _generation)
                {
                    _scheduled = false;
                    if (!_disposed && _waiting.Count > 0) Schedule();
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _generation++;
            foreach (var pending in _pending.Values) pending.TrySetResult(null);
            _pending.Clear();
            _waiting.Clear();
        }
        _scope.Cancel();
        _scope.Dispose();
    }
}
