// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播弹幕准入策略：优先级、同文案合并、每秒速率、队列淘汰、排队超时与计数。
/// 全部通过显式时间参数驱动，不依赖真实时钟。
/// </summary>
public sealed class LiveDanmakuAdmissionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Priority_RanksSuperChatAboveGuardsAboveHighlightedAboveNormal()
    {
        var normal = Candidate("普通");
        var highlightedByAdmin = Candidate("管理员", isAdmin: true);
        var highlightedByVip = Candidate("老爷", isVip: true);
        var highlightedByKeyword = Candidate("关键词", isKeywordHighlighted: true);
        var guard1 = Candidate("总督", guardLevel: 1);
        var guard2 = Candidate("提督", guardLevel: 2);
        var guard3 = Candidate("舰长", guardLevel: 3);
        var superChat = Candidate("醒目留言", isSuperChat: true);

        Assert.Equal(LiveDanmakuPriority.Normal, normal.Priority);
        Assert.Equal(LiveDanmakuPriority.Highlighted, highlightedByAdmin.Priority);
        Assert.Equal(LiveDanmakuPriority.Highlighted, highlightedByVip.Priority);
        Assert.Equal(LiveDanmakuPriority.Highlighted, highlightedByKeyword.Priority);
        Assert.Equal(LiveDanmakuPriority.GuardLevel1, guard1.Priority);
        Assert.Equal(LiveDanmakuPriority.GuardLevel2, guard2.Priority);
        Assert.Equal(LiveDanmakuPriority.GuardLevel3, guard3.Priority);
        Assert.Equal(LiveDanmakuPriority.SuperChat, superChat.Priority);
        Assert.True(superChat.Priority > guard3.Priority);
        Assert.True(guard3.Priority > guard2.Priority);
        Assert.True(guard2.Priority > guard1.Priority);
        Assert.True(guard1.Priority > highlightedByKeyword.Priority);
        Assert.True(highlightedByKeyword.Priority > normal.Priority);
    }

    [Fact]
    public void Submit_EnqueuesSingleNormalMessage()
    {
        var admission = Create();

        var result = admission.Submit(Candidate("666"), T0);

        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, result.Action);
        Assert.Equal(1, admission.PendingCount);
        Assert.Equal(["666"], admission.Drain(T0, 10));
    }

    [Fact]
    public void DuplicateTextWithinMergeWindow_IsMergedIntoOneDisplayedCount()
    {
        var admission = Create(mergeWindow: TimeSpan.FromSeconds(2));

        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, admission.Submit(Candidate("666"), T0).Action);
        var merged = admission.Submit(Candidate("666"), T0.AddMilliseconds(500));

        Assert.Equal(LiveDanmakuAdmissionAction.Merged, merged.Action);
        Assert.Equal(2, merged.MergedCount);
        Assert.Equal(["666 ×2"], admission.Drain(T0.AddMilliseconds(600), 10));

        var counters = admission.Snapshot();
        Assert.Equal(1, counters.Shown);
        Assert.Equal(1, counters.Merged);
    }

    [Fact]
    public void DuplicateTextAfterMergeWindow_IsDisplayedAgain()
    {
        var admission = Create(mergeWindow: TimeSpan.FromSeconds(2));

        admission.Submit(Candidate("666"), T0);
        Assert.Equal(["666"], admission.Drain(T0, 10));

        var later = admission.Submit(Candidate("666"), T0.AddSeconds(3));

        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, later.Action);
        Assert.Equal(["666"], admission.Drain(T0.AddSeconds(3), 10));
    }

    [Fact]
    public void DuplicateTextWithinMergeWindowAfterShown_IsMergedWithoutReinjection()
    {
        var admission = Create(mergeWindow: TimeSpan.FromSeconds(2));
        admission.Submit(Candidate("666"), T0);
        admission.Drain(T0, 10);

        var merged = admission.Submit(Candidate("666"), T0.AddSeconds(1));

        Assert.Equal(LiveDanmakuAdmissionAction.Merged, merged.Action);
        Assert.Empty(admission.Drain(T0.AddSeconds(1), 10));
        Assert.Equal(1, admission.Snapshot().Merged);
        Assert.Equal(1, admission.Snapshot().Shown);
    }

    [Fact]
    public void DuplicateTextOfDifferentCaseOrWhitespace_IsNormalizedBeforeMerging()
    {
        var admission = Create(mergeWindow: TimeSpan.FromSeconds(2));

        admission.Submit(Candidate("  666  "), T0);
        var merged = admission.Submit(Candidate("666"), T0.AddMilliseconds(100));

        Assert.Equal(LiveDanmakuAdmissionAction.Merged, merged.Action);
    }

    [Fact]
    public void RateLimit_DropsNormalMessagesBeyondPerSecondBudget()
    {
        var admission = Create(ratePerSecond: 3, mergeWindow: TimeSpan.Zero);

        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, admission.Submit(Candidate("a"), T0).Action);
        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, admission.Submit(Candidate("b"), T0).Action);
        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, admission.Submit(Candidate("c"), T0).Action);
        Assert.Equal(LiveDanmakuAdmissionAction.RateDropped, admission.Submit(Candidate("d"), T0).Action);

        Assert.Equal(1, admission.Snapshot().RateDropped);
        Assert.Equal(3, admission.PendingCount);
    }

    [Fact]
    public void RateLimit_RefillsAfterOneSecond()
    {
        var admission = Create(ratePerSecond: 2, mergeWindow: TimeSpan.Zero);
        admission.Submit(Candidate("a"), T0);
        admission.Submit(Candidate("b"), T0);
        Assert.Equal(LiveDanmakuAdmissionAction.RateDropped, admission.Submit(Candidate("c"), T0).Action);

        var later = admission.Submit(Candidate("d"), T0.AddSeconds(1));

        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, later.Action);
    }

    [Fact]
    public void PriorityMessages_AreNotSubjectToNormalRateLimit()
    {
        var admission = Create(ratePerSecond: 1, mergeWindow: TimeSpan.Zero);
        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, admission.Submit(Candidate("normal"), T0).Action);

        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, admission.Submit(Candidate("guard", guardLevel: 3), T0).Action);
        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, admission.Submit(Candidate("sc", isSuperChat: true), T0).Action);

        Assert.Equal(0, admission.Snapshot().RateDropped);
        Assert.Equal(3, admission.PendingCount);
    }

    [Fact]
    public void QueueFull_EvictsOldestLowestPriorityEntry()
    {
        var admission = Create(queueCapacity: 2, ratePerSecond: 100, mergeWindow: TimeSpan.Zero);
        admission.Submit(Candidate("first"), T0);
        admission.Submit(Candidate("second"), T0.AddMilliseconds(1));

        var result = admission.Submit(Candidate("guard", guardLevel: 3), T0.AddMilliseconds(2));

        Assert.Equal(LiveDanmakuAdmissionAction.Enqueued, result.Action);
        Assert.Equal(1, admission.Snapshot().QueueDropped);
        Assert.Equal(["guard", "second"], admission.Drain(T0.AddMilliseconds(3), 10));
    }

    [Fact]
    public void QueueFull_DropsIncomingWhenNoLowerPriorityExists()
    {
        var admission = Create(queueCapacity: 1, ratePerSecond: 100, mergeWindow: TimeSpan.Zero);
        admission.Submit(Candidate("guard", guardLevel: 3), T0);

        var result = admission.Submit(Candidate("ordinary"), T0.AddMilliseconds(1));

        Assert.Equal(LiveDanmakuAdmissionAction.QueueDropped, result.Action);
        Assert.Equal(1, admission.Snapshot().QueueDropped);
        Assert.Equal(["guard"], admission.Drain(T0.AddMilliseconds(2), 10));
    }

    [Fact]
    public void UndrainedMessage_DropsAfterEnqueueTimeout()
    {
        var admission = Create(ratePerSecond: 100, mergeWindow: TimeSpan.Zero, enqueueTimeout: TimeSpan.FromSeconds(1));
        admission.Submit(Candidate("late"), T0);

        Assert.Empty(admission.Drain(T0.AddSeconds(2), 10));
        Assert.Equal(1, admission.Snapshot().TimeoutDropped);
        Assert.Equal(0, admission.PendingCount);
    }

    [Fact]
    public void Drain_ReturnsHighestPriorityFirst()
    {
        var admission = Create(ratePerSecond: 100, mergeWindow: TimeSpan.FromSeconds(2));
        admission.Submit(Candidate("normal"), T0);
        admission.Submit(Candidate("guard1", guardLevel: 1), T0.AddMilliseconds(1));
        admission.Submit(Candidate("super-chat", isSuperChat: true), T0.AddMilliseconds(2));
        admission.Submit(Candidate("keyword", isKeywordHighlighted: true), T0.AddMilliseconds(3));

        Assert.Equal(["super-chat", "guard1", "keyword", "normal"], admission.Drain(T0.AddMilliseconds(4), 10));
    }

    [Fact]
    public void Drain_IsBoundedByMaxCount()
    {
        var admission = Create(ratePerSecond: 100, mergeWindow: TimeSpan.Zero);
        for (var i = 0; i < 10; i++)
        {
            admission.Submit(Candidate($"m{i}"), T0.AddMilliseconds(i));
        }

        Assert.Equal(3, admission.Drain(T0.AddMilliseconds(10), 3).Count);
        Assert.Equal(7, admission.PendingCount);
        Assert.Equal(3, admission.Drain(T0.AddMilliseconds(11), 3).Count);
        Assert.Equal(4, admission.PendingCount);
    }

    [Fact]
    public void BlankText_IsBlockedAndCounted()
    {
        var admission = Create();

        var result = admission.Submit(Candidate("   "), T0);

        Assert.Equal(LiveDanmakuAdmissionAction.Blocked, result.Action);
        Assert.Equal(1, admission.Snapshot().Blocked);
        Assert.Equal(0, admission.PendingCount);
    }

    [Fact]
    public void Counters_AccountForEveryReceivedCandidate()
    {
        var admission = Create(queueCapacity: 3, ratePerSecond: 4, mergeWindow: TimeSpan.FromSeconds(2), enqueueTimeout: TimeSpan.FromSeconds(1));
        admission.Submit(Candidate("same"), T0);
        admission.Submit(Candidate("same"), T0.AddMilliseconds(10));
        admission.Submit(Candidate("a"), T0.AddMilliseconds(20));
        admission.Submit(Candidate("b"), T0.AddMilliseconds(30));
        admission.Submit(Candidate("c"), T0.AddMilliseconds(40));
        admission.Submit(Candidate("d"), T0.AddMilliseconds(50));
        admission.Submit(Candidate("guard", guardLevel: 3), T0.AddMilliseconds(60));
        admission.Submit(Candidate("guard2", guardLevel: 3), T0.AddMilliseconds(70));
        admission.Submit(Candidate("   "), T0.AddMilliseconds(80));

        var counters = admission.Snapshot();
        var accounted = counters.Shown + counters.Merged + counters.Blocked + counters.RateDropped + counters.QueueDropped + counters.TimeoutDropped + admission.PendingCount;

        Assert.Equal(counters.Received, accounted);
        Assert.Equal(9, counters.Received);
        Assert.True(counters.RateDropped > 0);
        Assert.True(counters.QueueDropped > 0);
    }

    [Fact]
    public void FloodOfOneHundredThousand_KeepsQueueBoundedAndDropsDeterministically()
    {
        var options = new LiveDanmakuAdmissionOptions
        {
            QueueCapacity = 32,
            MergeWindow = TimeSpan.FromSeconds(2),
            NormalAdmissionsPerSecond = 20,
            EnqueueTimeout = TimeSpan.FromSeconds(3),
        };
        var admission = new LiveDanmakuAdmission(options);
        var now = T0;

        for (var i = 0; i < 100_000; i++)
        {
            now = T0.AddMilliseconds(i);
            var candidate = i % 100 == 0
                ? new LiveDanmakuCandidate { Text = $"guard-{i}", GuardLevel = 3 }
                : new LiveDanmakuCandidate { Text = $"normal-{i}" };
            admission.Submit(candidate, now);

            if (i % 1024 == 0)
            {
                Assert.True(admission.PendingCount <= options.QueueCapacity);
            }
        }

        var counters = admission.Snapshot();
        Assert.True(admission.PendingCount <= options.QueueCapacity);
        Assert.Equal(100_000, counters.Received);
        Assert.True(counters.RateDropped > 0);
        Assert.True(counters.QueueDropped > 0);
        Assert.True(counters.TimeoutDropped > 0);
        Assert.Equal(counters.Received, counters.Shown + counters.Merged + counters.Blocked + counters.RateDropped + counters.QueueDropped + counters.TimeoutDropped + admission.PendingCount);

        var drained = admission.Drain(now, 1000);
        Assert.True(drained.Count <= 1000);
        Assert.True(drained.Count <= options.QueueCapacity);
    }

    [Fact]
    public void SimulatedTenMinuteSoak_KeepsAdmissionStateBounded()
    {
        // 10 分钟、约 600 条/秒的固定节奏：状态只应有界队列与计数增长，不应堆积待处理消息。
        var options = new LiveDanmakuAdmissionOptions
        {
            QueueCapacity = 200,
            MergeWindow = TimeSpan.FromSeconds(2),
            NormalAdmissionsPerSecond = 20,
            EnqueueTimeout = TimeSpan.FromSeconds(3),
        };
        var admission = new LiveDanmakuAdmission(options);
        var random = new Random(196);
        var maxPending = 0;
        var shown = 0;
        const int tickMilliseconds = 33;
        const int ticks = (10 * 60 * 1000) / tickMilliseconds;

        for (var tick = 0; tick < ticks; tick++)
        {
            var now = T0.AddMilliseconds(tick * (long)tickMilliseconds);
            for (var i = 0; i < 20; i++)
            {
                var roll = random.Next(100);
                var candidate = roll == 0
                    ? new LiveDanmakuCandidate { Text = $"guard-{random.Next(100_000)}", GuardLevel = 3 }
                    : roll < 40
                        ? new LiveDanmakuCandidate { Text = $"重复-{random.Next(5)}" }
                        : new LiveDanmakuCandidate { Text = $"m-{random.Next(1_000_000)}" };
                admission.Submit(candidate, now);
            }

            shown += admission.Drain(now, 16).Count;
            maxPending = Math.Max(maxPending, admission.PendingCount);
        }

        var counters = admission.Snapshot();
        Assert.True(maxPending <= options.QueueCapacity);
        Assert.True(shown > 0);
        Assert.True(counters.Received > 300_000);
        Assert.True(counters.Merged > 0);
        Assert.True(counters.RateDropped > 0);
        Assert.Equal(counters.Received, counters.Shown + counters.Merged + counters.Blocked + counters.RateDropped + counters.QueueDropped + counters.TimeoutDropped + admission.PendingCount);
    }

    private static LiveDanmakuAdmission Create(
        int queueCapacity = 100,
        int ratePerSecond = 100,
        TimeSpan? mergeWindow = null,
        TimeSpan? enqueueTimeout = null)
        => new(new LiveDanmakuAdmissionOptions
        {
            QueueCapacity = queueCapacity,
            NormalAdmissionsPerSecond = ratePerSecond,
            MergeWindow = mergeWindow ?? TimeSpan.FromSeconds(2),
            EnqueueTimeout = enqueueTimeout ?? TimeSpan.FromSeconds(3),
        });

    private static LiveDanmakuCandidate Candidate(
        string text,
        int guardLevel = 0,
        bool isAdmin = false,
        bool isVip = false,
        bool isKeywordHighlighted = false,
        bool isSuperChat = false)
        => new()
        {
            Text = text,
            UserName = "user",
            GuardLevel = guardLevel,
            IsAdmin = isAdmin,
            IsVip = isVip,
            IsKeywordHighlighted = isKeywordHighlighted,
            IsSuperChat = isSuperChat,
        };
}
