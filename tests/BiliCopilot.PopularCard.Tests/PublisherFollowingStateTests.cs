// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

public sealed class PublisherFollowingStateTests
{
    [Fact]
    public async Task OldCardRemountConsumesNewerFeedWithoutReplayingItsSnapshot()
    {
        using var batcher = new UserRelationBatcher((_, _) => throw new InvalidOperationException("Fresh feed should avoid HTTP."));
        var old = new PublisherFollowingState(batcher, "1000", "42", false);
        var newer = new PublisherFollowingState(batcher, "1000", "42", true);
        Assert.True(newer.Current);
        Assert.True(await old.LoadAsync("1000"));
        Assert.True(batcher.GetKnownFollowing("1000", "42"));
    }

    [Fact]
    public async Task RemountDoesNotExtendFeedCacheLifetime()
    {
        var clock = new ManualClock();
        var calls = 0;
        using var batcher = new UserRelationBatcher((ids, _) =>
        {
            calls++;
            return Task.FromResult<IReadOnlyDictionary<string, UserRelationStatus>>(ids.ToDictionary(id => id, _ => UserRelationStatus.Unfollow));
        }, clock);
        var state = new PublisherFollowingState(batcher, "1000", "42", true);
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.True(await state.LoadAsync("1000"));
        Assert.Equal(0, calls);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(await state.LoadAsync("1000"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task EvictionAndFailedRefreshPreserveSameAccountSuccess()
    {
        var calls = 0;
        using var batcher = new UserRelationBatcher((ids, _) =>
        {
            if (++calls > 1) throw new HttpRequestException("offline");
            return Task.FromResult<IReadOnlyDictionary<string, UserRelationStatus>>(ids.ToDictionary(id => id, _ => UserRelationStatus.Following));
        });
        var state = new PublisherFollowingState(batcher, "1000", "42", null);
        Assert.True(await state.LoadAsync("1000"));
        for (var id = 2000; id < 2256; id++) batcher.SetKnownFollowing("1000", id.ToString(), false);
        Assert.Null(batcher.GetKnownFollowing("1000", "42"));
        Assert.True(await state.LoadAsync("1000"));
        Assert.True(state.Current);
        Assert.Equal(2, calls);
        Assert.Null(await state.LoadAsync(null));
        Assert.Null(await state.LoadAsync("3000"));
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
