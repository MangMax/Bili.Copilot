// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 音量落盘缓冲：连续变更只提交最后一次，尾部变更不会丢失。
/// </summary>
public class VolumeCommitTrackerTests
{
    [Fact]
    public void TryTakePending_ReturnsFalse_WhenNoVolumeRecorded()
    {
        var tracker = new VolumeCommitTracker();

        Assert.False(tracker.TryTakePending(out _));
    }

    [Fact]
    public void TryTakePending_ReturnsLatestVolume_WhenChangesAreRecordedInSequence()
    {
        var tracker = new VolumeCommitTracker();
        tracker.Record(10);
        tracker.Record(35);
        tracker.Record(80);

        Assert.True(tracker.TryTakePending(out var volume));
        Assert.Equal(80, volume);
        Assert.False(tracker.TryTakePending(out _));
    }

    [Fact]
    public void TryTakePending_OnlyCommitsOnce_ForCoalescedChanges()
    {
        var tracker = new VolumeCommitTracker();
        tracker.Record(15);
        tracker.Record(60);

        Assert.True(tracker.TryTakePending(out _));
        Assert.False(tracker.TryTakePending(out _));
    }

    [Fact]
    public void TryTakePending_CommitsMutedVolume()
    {
        var tracker = new VolumeCommitTracker();
        tracker.Record(0);

        Assert.True(tracker.TryTakePending(out var volume));
        Assert.Equal(0, volume);
    }

    [Fact]
    public void Record_StartsNewBatch_AfterPreviousCommit()
    {
        var tracker = new VolumeCommitTracker();
        tracker.Record(20);
        Assert.True(tracker.TryTakePending(out var first));

        tracker.Record(70);
        Assert.True(tracker.TryTakePending(out var second));

        Assert.Equal(20, first);
        Assert.Equal(70, second);
    }
}
