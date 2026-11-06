// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

public sealed class UserRelationBatcherTests
{
    [Fact]
    public async Task DuplicateAuthorsShareOneFetchAndCache()
    {
        var calls = 0;
        using var batcher = new UserRelationBatcher((ids, _) =>
        {
            Interlocked.Increment(ref calls);
            Assert.Single(ids);
            return Task.FromResult<IReadOnlyDictionary<string, UserRelationStatus>>(new Dictionary<string, UserRelationStatus> { ["42"] = UserRelationStatus.Following });
        });
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => batcher.QueryAsync("1000", "42"))).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(results, value => Assert.True(value));
        Assert.True(await batcher.QueryAsync("1000", "42"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task QueuedAuthorsAreDrainedInBoundedBatches()
    {
        var batches = new List<string[]>();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var batcher = new UserRelationBatcher(async (ids, _) =>
        {
            batches.Add(ids.ToArray());
            if (batches.Count == 1)
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
            }
            return ids.ToDictionary(id => id, _ => UserRelationStatus.Unfollow);
        });
        var first = batcher.QueryAsync("1000", "1");
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = Enumerable.Range(2, 94).Select(id => batcher.QueryAsync("1000", id.ToString())).ToArray();
        releaseFirst.SetResult();
        Assert.All(await Task.WhenAll(queued.Prepend(first)).WaitAsync(TimeSpan.FromSeconds(5)), value => Assert.False(value));
        Assert.All(batches, batch => Assert.InRange(batch.Length, 1, 40));
        Assert.Equal(95, batches.SelectMany(batch => batch).Distinct().Count());
        Assert.Equal(new[] { 1, 40, 40, 14 }, batches.Select(batch => batch.Length));
    }

    [Fact]
    public async Task AccountSwitchRejectsLateOldAccountResponse()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldResponse = new TaskCompletionSource<IReadOnlyDictionary<string, UserRelationStatus>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var batcher = new UserRelationBatcher((_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1) { started.SetResult(); return oldResponse.Task; }
            return Task.FromResult<IReadOnlyDictionary<string, UserRelationStatus>>(new Dictionary<string, UserRelationStatus> { ["42"] = UserRelationStatus.Unfollow });
        });
        var old = batcher.QueryAsync("1000", "42");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var current = batcher.QueryAsync("2000", "42");
        Assert.Null(await old);
        oldResponse.SetResult(new Dictionary<string, UserRelationStatus> { ["42"] = UserRelationStatus.Following });
        Assert.False(await current.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Null(batcher.GetKnownFollowing("1000", "42"));
        Assert.False(batcher.GetKnownFollowing("2000", "42"));
    }

    [Fact]
    public async Task CancelingOneCardDoesNotCancelSharedConsumer()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<IReadOnlyDictionary<string, UserRelationStatus>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken sharedToken = default;
        using var batcher = new UserRelationBatcher((_, token) => { sharedToken = token; started.SetResult(); return response.Task; });
        using var cancellation = new CancellationTokenSource();
        var first = batcher.QueryAsync("1000", "42", cancellation.Token);
        var second = batcher.QueryAsync("1000", "42");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(sharedToken.IsCancellationRequested);
        response.SetResult(new Dictionary<string, UserRelationStatus> { ["42"] = UserRelationStatus.Following });
        Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task FeedStateWinsOverOlderPendingResponse()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<IReadOnlyDictionary<string, UserRelationStatus>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var batcher = new UserRelationBatcher((_, _) => { started.SetResult(); return response.Task; });
        var query = batcher.QueryAsync("1000", "42");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        batcher.SetKnownFollowing("1000", "42", true);
        response.SetResult(new Dictionary<string, UserRelationStatus> { ["42"] = UserRelationStatus.Unfollow });
        Assert.True(await query.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(batcher.GetKnownFollowing("1000", "42"));
    }

    [Fact]
    public async Task FailedRefreshPreservesKnownStateAndBacksOffUnknownUsers()
    {
        var clock = new ManualClock();
        var calls = 0;
        using var batcher = new UserRelationBatcher((ids, _) =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 2) throw new HttpRequestException("rejected");
            return Task.FromResult<IReadOnlyDictionary<string, UserRelationStatus>>(ids.ToDictionary(id => id,
                _ => call == 1 ? UserRelationStatus.Following : UserRelationStatus.Unfollow));
        }, clock);
        Assert.True(await batcher.QueryAsync("1000", "42"));
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True(await batcher.QueryAsync("1000", "42"));
        Assert.Null(await batcher.QueryAsync("1000", "99"));
        Assert.Equal(2, calls);
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.False(await batcher.QueryAsync("1000", "99"));
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task UnknownRelationAndLoggedOutAccountStayUnknown()
    {
        var calls = 0;
        using var batcher = new UserRelationBatcher((ids, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult<IReadOnlyDictionary<string, UserRelationStatus>>(ids.ToDictionary(id => id, _ => UserRelationStatus.Unknown));
        });
        Assert.Null(await batcher.QueryAsync(null, "42"));
        Assert.Null(await batcher.QueryAsync("1000", "1000"));
        Assert.Null(await batcher.QueryAsync("1000", "0"));
        Assert.Equal(0, calls);
        Assert.Null(await batcher.QueryAsync("1000", "42"));
        Assert.Null(batcher.GetKnownFollowing("1000", "42"));
        Assert.Equal(1, calls);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
