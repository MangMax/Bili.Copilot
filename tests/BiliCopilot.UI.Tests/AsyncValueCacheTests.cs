// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class AsyncValueCacheTests
{
    [Fact]
    public async Task ConcurrentPickersSharePendingLoadAndCompletedValue()
    {
        var cache = new AsyncValueCache<string[]>();
        var completion = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task<string[]> Load()
        {
            Interlocked.Increment(ref calls);
            return completion.Task;
        }

        var first = cache.GetAsync(Load);
        var second = cache.GetAsync(Load);
        Assert.Same(first, second);
        Assert.Equal(1, calls);
        string[] fonts = ["Saved family", "Segoe UI"];
        completion.SetResult(fonts);
        Assert.Same(fonts, await first);
        Assert.Same(fonts, await cache.GetAsync(Load));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FailedLoadCanRetryWithoutCachingTheFailure()
    {
        var cache = new AsyncValueCache<string[]>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(
            () => Task.FromException<string[]>(new InvalidOperationException())));
        var result = await cache.GetAsync(() => Task.FromResult(new[] { "Recovered" }));
        Assert.Equal(["Recovered"], result);
    }

    [Fact]
    public async Task CancelledLoadCanRetry()
    {
        var cache = new AsyncValueCache<int>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync(
            () => Task.FromCanceled<int>(new CancellationToken(true))));
        Assert.Equal(42, await cache.GetAsync(() => Task.FromResult(42)));
    }

    [Fact]
    public async Task EmptyCatalogIsACompletedValue()
    {
        var cache = new AsyncValueCache<string[]>();
        Assert.Empty(await cache.GetAsync(() => Task.FromResult(Array.Empty<string>())));
        Assert.Empty(await cache.GetAsync(() => throw new InvalidOperationException("Must not reload")));
    }
}
