// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Danmaku;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Danmaku;

namespace BiliCopilot.UI.Tests;

public sealed class DanmakuLoadSessionTests
{
    [Fact]
    public async Task Switch_LateOldResult_OnlyCurrentSessionPublishes()
    {
        var service = new ControlledService();
        var queued = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var published = new List<DanmakuLoadState>();
        using var loader = new DanmakuLoadSession(service, queued.Enqueue, published.Add);
        var old = loader.LoadAsync("1", "11", 1);
        var current = loader.LoadAsync("2", "22", 1);
        Assert.Equal(("1", "11", 1), (service.Requests[0].Aid, service.Requests[0].Cid, service.Requests[0].Index));
        Assert.Equal(("2", "22", 1), (service.Requests[1].Aid, service.Requests[1].Cid, service.Requests[1].Index));
        Assert.True(service.Requests[0].Token.IsCancellationRequested);
        service.Requests[1].Complete("new");
        await current;
        service.Requests[0].Complete("old"); // Service deliberately ignores cancellation.
        await old;
        while (queued.TryDequeue(out var action)) action();
        Assert.Equal(2, published.Count);
        Assert.True(published[0].IsLoading);
        Assert.False(published[1].IsLoading);
        Assert.Equal("new", Assert.Single(published[1].Items!).Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrClose_SixActiveAndQueuedSegments_StopsSchedulingAndPublishing(bool close)
    {
        var service = new ControlledService();
        var published = new System.Collections.Concurrent.ConcurrentQueue<DanmakuLoadState>();
        using var loader = new DanmakuLoadSession(service, action => action(), published.Enqueue);
        var load = loader.LoadAsync("1", "11", 20);
        Assert.Equal(6, service.Requests.Count);
        if (close) loader.Dispose();
        else loader.Cancel();
        Assert.All(service.Requests, request => Assert.True(request.Token.IsCancellationRequested));
        foreach (var request in service.Requests.ToArray()) request.Complete("late");
        await load.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(6, service.Requests.Count);
        Assert.True(Assert.Single(published).IsLoading);
    }

    [Fact]
    public async Task Close_QueuedCompletionAndLateFailure_PublishesNothingAndRejectsNewLoads()
    {
        var service = new ControlledService();
        var queued = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var published = new List<DanmakuLoadState>();
        using var loader = new DanmakuLoadSession(service, queued.Enqueue, published.Add);
        var old = loader.LoadAsync("1", "11", 1);
        var current = loader.LoadAsync("2", "22", 1);
        service.Requests[1].Complete("new");
        await current;
        loader.Dispose();
        service.Requests[0].Result.SetException(new InvalidOperationException("late wrapped cancellation"));
        await old;
        await loader.LoadAsync("3", "33", 1);
        while (queued.TryDequeue(out var action)) action();
        Assert.Empty(published);
        Assert.Equal(2, service.Requests.Count);
    }

    [Fact]
    public async Task Reload_ContinuousSameMediaLoads_OnlyLastResultAndCountPublish()
    {
        var service = new ControlledService();
        var queued = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var published = new List<DanmakuLoadState>();
        using var loader = new DanmakuLoadSession(service, queued.Enqueue, published.Add);
        var loads = Enumerable.Range(0, 5).Select(_ => loader.LoadAsync("1", "11", 1)).ToArray();
        for (var i = 4; i >= 0; i--) service.Requests[i].Complete($"result-{i}");
        await Task.WhenAll(loads);
        while (queued.TryDequeue(out var action)) action();
        Assert.Equal(2, published.Count);
        Assert.Equal("result-4", Assert.Single(published[1].Items!).Content);
        Assert.All(service.Requests.Take(4), request => Assert.True(request.Token.IsCancellationRequested));
    }

    [Fact]
    public async Task Load_FailedSegment_ReleasesPermitAndPublishesRemainingSegments()
    {
        var service = new ControlledService();
        var published = new System.Collections.Concurrent.ConcurrentQueue<DanmakuLoadState>();
        using var loader = new DanmakuLoadSession(service, action => action(), published.Enqueue);
        var load = loader.LoadAsync("1", "11", 20);
        Assert.Equal(6, service.Requests.Count);
        for (var index = 0; index < 20; index++)
        {
            var request = await service.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (index == 0) request.Result.SetException(new InvalidOperationException("segment failed"));
            else request.Complete("ok");
        }

        await load.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(20, service.Requests.Count);
        Assert.Equal(Enumerable.Range(1, 20), service.Requests.Select(request => request.Index).Order());
        Assert.Equal(6, service.MaximumActive);
        Assert.Equal(0, service.Active);
        Assert.Single(published, state => state.Error is not null);
        var final = Assert.Single(published, state => !state.IsLoading);
        Assert.Equal(19, final.Items!.Count);
    }

    [Fact]
    public async Task Load_EmptyMedia_CompletesWithEmptyCountAndNoRequests()
    {
        var service = new ControlledService();
        var published = new List<DanmakuLoadState>();
        using var loader = new DanmakuLoadSession(service, action => action(), published.Add);
        await loader.LoadAsync("1", "11", 0);
        Assert.Empty(service.Requests);
        Assert.Equal(2, published.Count);
        Assert.False(published[1].IsLoading);
        Assert.Empty(published[1].Items!);
    }

    private sealed record Request(string Aid, string Cid, int Index, CancellationToken Token, TaskCompletionSource<IReadOnlyList<DanmakuInformation>> Result)
    {
        public void Complete(string text) => Result.SetResult([new DanmakuInformation("1", text, 1, 0, 0, 25)]);
    }

    private sealed class ControlledService : IDanmakuService
    {
        public List<Request> Requests { get; } = [];
        public System.Threading.Channels.Channel<Request> Started { get; } = System.Threading.Channels.Channel.CreateUnbounded<Request>();
        private int _active;
        public int Active => _active;
        public int MaximumActive { get; private set; }
        public async Task<IReadOnlyList<DanmakuInformation>> GetSegmentDanmakusWithGrpcAsync(string aid, string cid, int segmentIndex, CancellationToken cancellationToken = default)
        {
            var request = new Request(aid, cid, segmentIndex, cancellationToken, new(TaskCreationOptions.RunContinuationsAsynchronously));
            Requests.Add(request);
            MaximumActive = Math.Max(MaximumActive, Interlocked.Increment(ref _active));
            Started.Writer.TryWrite(request);
            try
            {
                return await request.Result.Task;
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public Task<DanmakuMeta> GetDanmakuMetaAsync(string aid, string cid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DanmakuInformation>> GetSegmentDanmakusWithWebAsync(string aid, string cid, int segmentIndex, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendVideoDanmakuAsync(string content, string aid, string cid, int progress, string color, bool isStandardSize = true, DanmakuLocation location = DanmakuLocation.Scroll, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendLiveDanmakuAsync(string content, string roomId, string color, bool isStandardSize = true, DanmakuLocation location = DanmakuLocation.Scroll, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
