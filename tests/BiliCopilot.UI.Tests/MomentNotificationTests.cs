// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Services.MomentNotifications;

namespace BiliCopilot.UI.Tests;

public sealed class MomentNotificationTests
{
    [Fact]
    public async Task FirstSuccessfulScanIsSilentAndLaterPublicationsAreReported()
    {
        var first = await MomentNotificationScan.ReadAsync(null,
            (_, _, _) => Task.FromResult(new MomentScanPage([Item(20), Item(10)], null, false)), default);
        var later = await MomentNotificationScan.ReadAsync(first.Baseline,
            (_, _, _) => Task.FromResult(new MomentScanPage([Item(30), Item(20)], null, false)), default);

        Assert.Empty(first.Notices);
        Assert.Equal(30UL, later.Baseline);
        Assert.Equal(30UL, Assert.Single(later.Notices).Id);
    }

    [Fact]
    public async Task ScanFindsUpdatesBeyondFirstPageDespiteOldPinnedAndDuplicateItems()
    {
        var first = new MomentScanPage([Item(160), Item(150), Item(140), Item(20, pinned: true)], "next", true);
        var second = new MomentScanPage([Item(140), Item(130), Item(120), Item(100)], null, false);
        var result = await MomentNotificationScan.ReadAsync(100,
            (offset, _, _) => Task.FromResult(offset is null ? first : second), default);

        Assert.Equal(160UL, result.Baseline);
        Assert.Equal(new ulong[] { 120, 130, 140, 150, 160 }, result.Notices.Select(notice => notice.Id).Order());
    }

    [Fact]
    public async Task RepeatedCursorFailsWithoutReturningAPartialScan()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => MomentNotificationScan.ReadAsync(100,
            (_, _, _) => Task.FromResult(new MomentScanPage([Item(160)], "same", true)), default));
    }

    [Fact]
    public async Task AnOldItemAtTheEndOfAMixedPageDoesNotHideOverlappingUpdates()
    {
        var first = new MomentScanPage([Item(160), Item(100)], "next", true);
        var second = new MomentScanPage([Item(150), Item(90)], null, false);
        var result = await MomentNotificationScan.ReadAsync(100,
            (offset, _, _) => Task.FromResult(offset is null ? first : second), default);

        Assert.Equal(new ulong[] { 150, 160 }, result.Notices.Select(notice => notice.Id).Order());
    }

    [Fact]
    public void DeliveryQueuesThreeDetailsAndSummarizesTheRestWithoutDuplicates()
    {
        var state = new MomentNotificationState();
        var notices = new[] { Notice(102), Notice(109), Notice(108), Notice(107), Notice(106), Notice(107) };
        state.PrepareDelivery(notices, _ => true, new Dictionary<long, bool>());
        state.PrepareDelivery(notices, _ => true, new Dictionary<long, bool>());

        Assert.Equal(new ulong[] { 109, 108, 107 }, state.Queue.Where(item => !item.InSummary).Select(item => item.Notice.Id));
        Assert.Equal(new ulong[] { 106, 102 }, state.Queue.Where(item => item.InSummary).Select(item => item.Notice.Id));
    }

    [Fact]
    public void MissingRedDotKeepsPendingUpdateUntilAnObservedDotTurnsOff()
    {
        var state = new MomentNotificationState();
        state.PrepareDelivery([Notice(109, author: 1), Notice(108, author: 2)], _ => true,
            new Dictionary<long, bool> { [1] = true });
        state.PrepareDelivery([], _ => true, new Dictionary<long, bool>());
        Assert.Equal(2, state.Queue.Count);

        state.PrepareDelivery([], _ => true, new Dictionary<long, bool> { [1] = false, [2] = false });

        Assert.Equal(108UL, Assert.Single(state.Queue).Notice.Id);
    }

    private static MomentScanItem Item(ulong id, bool pinned = false)
        => new(id, pinned, Notice(id));

    private static MomentNotice Notice(ulong id, long author = 1)
        => new(id, author, "Author", "Post", MomentNoticeKind.Dynamic);
}
