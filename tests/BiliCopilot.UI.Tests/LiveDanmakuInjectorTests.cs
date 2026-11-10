// Copyright (c) Bili Copilot. All rights reserved.

using System.Threading.Channels;
using BiliCopilot.UI.Live;
using BiliCopilot.UI.Tests.Support;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播注入层：事件按类型转成准入候选，批量消费后一次交付，不逐条调度；
/// 关键词高亮、醒目留言优先与超时丢弃都由准入策略保证。
/// </summary>
public sealed class LiveDanmakuInjectorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DanmakuEvent_BecomesInjectedText()
    {
        var admission = Create();
        var batches = new List<IReadOnlyList<string>>();
        var injector = new LiveDanmakuInjector(admission, batches.Add);

        injector.Submit(TestLiveEvents.Danmaku("hello", guardLevel: 3), T0);
        var injected = injector.Flush(T0);

        Assert.Equal(1, injected);
        var batch = Assert.Single(batches);
        Assert.Equal(["hello"], batch);
        Assert.Equal(1, admission.Snapshot().Shown);
    }

    [Fact]
    public void Flush_DeliversOneBatchForManyMessages()
    {
        var admission = Create();
        var batches = new List<IReadOnlyList<string>>();
        var injector = new LiveDanmakuInjector(admission, batches.Add);

        injector.Submit(TestLiveEvents.Danmaku("a"), T0);
        injector.Submit(TestLiveEvents.Danmaku("b"), T0.AddMilliseconds(1));
        injector.Submit(TestLiveEvents.Danmaku("c"), T0.AddMilliseconds(2));

        Assert.Equal(3, injector.Flush(T0.AddMilliseconds(3)));
        var batch = Assert.Single(batches);
        Assert.Equal(3, batch.Count);
    }

    [Fact]
    public void Flush_DoesNotInvokeInjectionWhenNothingWasAdmitted()
    {
        var admission = Create();
        var called = 0;
        var injector = new LiveDanmakuInjector(admission, _ => called++);

        Assert.Equal(0, injector.Flush(T0));
        Assert.Equal(0, called);
    }

    [Fact]
    public void SuperChatEvent_PreemptsQueuedNormalDanmaku()
    {
        var admission = Create(queueCapacity: 2, ratePerSecond: 100);
        var batches = new List<IReadOnlyList<string>>();
        var injector = new LiveDanmakuInjector(admission, batches.Add);
        injector.Submit(TestLiveEvents.Danmaku("normal-1"), T0);
        injector.Submit(TestLiveEvents.Danmaku("normal-2"), T0.AddMilliseconds(1));

        injector.Submit(TestLiveEvents.SuperChat("paid-message"), T0.AddMilliseconds(2));
        injector.Flush(T0.AddMilliseconds(3));

        var batch = Assert.Single(batches);
        Assert.Equal("paid-message", batch[0]);
        Assert.Equal(1, admission.Snapshot().QueueDropped);
    }

    [Fact]
    public void BlankDanmaku_IsBlocked()
    {
        var admission = Create();
        var batches = new List<IReadOnlyList<string>>();
        var injector = new LiveDanmakuInjector(admission, batches.Add);

        injector.Submit(TestLiveEvents.Danmaku("   "), T0);
        injector.Flush(T0);

        Assert.Empty(batches);
        Assert.Equal(1, admission.Snapshot().Blocked);
    }

    [Fact]
    public void KeywordHighlight_IsAppliedBeforeAdmission()
    {
        var admission = Create(ratePerSecond: 1);
        var injector = new LiveDanmakuInjector(admission, _ => { }, keyword => keyword.Contains("抽奖", StringComparison.Ordinal));
        injector.Submit(TestLiveEvents.Danmaku("normal"), T0);

        injector.Submit(TestLiveEvents.Danmaku("抽奖啦"), T0.AddMilliseconds(1));

        Assert.Equal(2, admission.PendingCount);
        Assert.Equal(0, admission.Snapshot().RateDropped);
    }

    [Fact]
    public void Flush_DropsCandidatesOlderThanEnqueueTimeout()
    {
        var admission = Create(enqueueTimeout: TimeSpan.FromSeconds(1));
        var batches = new List<IReadOnlyList<string>>();
        var injector = new LiveDanmakuInjector(admission, batches.Add);
        injector.Submit(TestLiveEvents.Danmaku("late"), T0);

        Assert.Equal(0, injector.Flush(T0.AddSeconds(2)));
        Assert.Empty(batches);
        Assert.Equal(1, admission.Snapshot().TimeoutDropped);
    }

    [Fact]
    public void SubmitAvailable_ConsumesTheWholeChannelInOnePass()
    {
        var admission = Create();
        var injector = new LiveDanmakuInjector(admission, _ => { });
        var channel = Channel.CreateBounded<LiveDanmakuEvent>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        for (var i = 0; i < 2; i++)
        {
            channel.Writer.TryWrite(TestLiveEvents.Danmaku($"m{i}"));
        }

        channel.Writer.TryComplete();
        Assert.Equal(2, injector.SubmitAvailable(channel.Reader, T0));
    }

    [Fact]
    public async Task RunAsync_BatchesChannelEventsAndStopsOnCancellation()
    {
        var admission = Create();
        var batches = new List<IReadOnlyList<string>>();
        var firstBatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var injector = new LiveDanmakuInjector(admission, batch =>
        {
            lock (batches)
            {
                batches.Add(batch);
            }

            firstBatch.TrySetResult();
        });
        var channel = Channel.CreateBounded<LiveDanmakuEvent>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        channel.Writer.TryWrite(TestLiveEvents.Danmaku("a"));
        channel.Writer.TryWrite(TestLiveEvents.Danmaku("b"));
        channel.Writer.TryWrite(TestLiveEvents.Danmaku("c"));
        using var cancellation = new CancellationTokenSource();

        var run = injector.RunAsync(channel.Reader, cancellation.Token);
        await firstBatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        lock (batches)
        {
            var batch = Assert.Single(batches);
            Assert.Equal(3, batch.Count);
        }
    }

    [Fact]
    public async Task PumpAndInjector_RouteKernelEventsIntoOnePriorityOrderedBatch()
    {
        var session = new FakeLiveDanmakuSession("123");
        var service = new FakeLiveDanmakuService { Session = session };
        await using var pump = new LiveEventPump(service);
        var admission = Create();
        var injected = new List<string>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var injector = new LiveDanmakuInjector(admission, batch =>
        {
            lock (injected)
            {
                injected.AddRange(batch);
                if (injected.Count >= 3)
                {
                    completed.TrySetResult();
                }
            }
        });
        Assert.True(await pump.StartAsync("123"));
        using var cancellation = new CancellationTokenSource();
        var run = injector.RunAsync(pump.Events, cancellation.Token);

        session.Emit(TestLiveEvents.Danmaku("normal"));
        session.Emit(TestLiveEvents.Danmaku("guard", guardLevel: 3));
        session.Emit(TestLiveEvents.SuperChat("paid"));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        lock (injected)
        {
            Assert.Equal(["paid", "guard", "normal"], injected);
        }

        Assert.Equal(3, admission.Snapshot().Shown);
    }

    private static LiveDanmakuAdmission Create(
        int queueCapacity = 100,
        int ratePerSecond = 100,
        TimeSpan? enqueueTimeout = null)
        => new(new LiveDanmakuAdmissionOptions
        {
            QueueCapacity = queueCapacity,
            NormalAdmissionsPerSecond = ratePerSecond,
            MergeWindow = TimeSpan.FromSeconds(2),
            EnqueueTimeout = enqueueTimeout ?? TimeSpan.FromSeconds(3),
        });
}
