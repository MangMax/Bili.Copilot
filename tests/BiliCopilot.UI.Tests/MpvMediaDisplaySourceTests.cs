using BiliCopilot.UI.Services;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Dispatching;
using Richasy.MpvKernel.Core;

namespace BiliCopilot.UI.Tests;

public sealed class MpvMediaDisplaySourceTests
{
    [Fact]
    public async Task SelectedStreamMetadataResizesBeforeLoadThenRuntimeCorrectsAndRejectsLateHints()
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = new CompactOverlayRatioSession(new(() => null, _ => { }), () => bounds,
            () => new(0, 0, 1920, 1080), value => bounds = value);
        using var source = new MpvMediaDisplaySource(client, queue, session.BeginMedia, session.UpdateMedia);
        session.SetCompact(true);
        source.Suspend();
        var publish = source.CreateMetadataPublisher();
        source.Resume();
        var pending = publish(1080, 1920);
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 360, 640), bounds);
        client.Loading();
        queue.Drain();
        bounds = new(100, 80, 500, 500); // a manual resize during load
        client.SetOutput(1, 1080, 1920);
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 500), bounds);
        pending = publish(1920, 1080); // equal-ratio runtime output still owns priority
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 500), bounds);
        client.SetOutput(1, 1440, 1080);
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 375), bounds);
        pending = publish(1080, 1920);
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 375), bounds);
    }

    [Fact]
    public async Task SwitchRejectsQueuedAndLateMetadataAndResetsRuntimePriority()
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = new CompactOverlayRatioSession(new(() => null, _ => { }), () => bounds,
            () => new(0, 0, 1920, 1080), value => bounds = value);
        using var source = new MpvMediaDisplaySource(client, queue, session.BeginMedia, session.UpdateMedia);
        session.SetCompact(true);
        var old = source.CreateMetadataPublisher();
        source.Resume();
        client.SetOutput(1, 1920, 1080);
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        var queued = old(1000, 1000);
        source.Suspend();
        bounds = new(100, 80, 500, 500);
        source.Resume();
        var current = source.CreateMetadataPublisher();
        queue.Drain();
        await queued;
        session.SetCompact(false);
        session.SetCompact(true); // old runtime ratio must also have been cleared
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 500), bounds);
        var pending = current(1080, 1920);
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 281, 500), bounds);
        pending = old(1920, 1080);
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 281, 500), bounds);
        client.End();
        pending = current(1000, 1000);
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 281, 500), bounds);
        source.Dispose();
        pending = current(1920, 1080);
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 281, 500), bounds);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(-1, 1080)]
    [InlineData(1920, 0)]
    [InlineData(double.NaN, 1080)]
    [InlineData(1920, double.PositiveInfinity)]
    public async Task InvalidHintsWaitAndMetadataRespectsToggleAndManualSize(double width, double height)
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        var preference = new CompactOverlayRatioPreference(() => false, _ => { });
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = new CompactOverlayRatioSession(preference, () => bounds,
            () => new(0, 0, 1920, 1080), value => bounds = value);
        using var source = new MpvMediaDisplaySource(client, queue, session.BeginMedia, session.UpdateMedia);
        session.SetCompact(true);
        source.Resume();
        var publish = source.CreateMetadataPublisher();
        preference.SetEnabled(true);
        var pending = publish(width, height);
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 400), bounds);
        preference.SetEnabled(false);
        pending = publish(1080, 1920);
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 400), bounds);
        preference.SetEnabled(true);
        Assert.Equal(new RatioWindowBounds(100, 80, 360, 640), bounds);
        bounds = new(100, 80, 500, 500);
        pending = publish(2160, 3840);
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 500), bounds);
        client.Loading();
        client.SetOutput(1, 0, 0);
        client.Loaded();
        queue.MediaTimer.Fire();
        pending = publish(1440, 1080); // invalid runtime output must not suppress usable hints
        queue.Drain();
        await pending;
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 375), bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReusedPlaylistSlotCannotPublishUntilNewEntryLoads(bool switchDuringSample)
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = new CompactOverlayRatioSession(new(() => null, _ => { }), () => bounds,
            () => new(0, 0, 1920, 1080), value => bounds = value);
        using var source = new MpvMediaDisplaySource(client, queue, session.BeginMedia, session.UpdateMedia);
        session.SetCompact(true);
        source.Resume();
        client.SetOutput(41, 1920, 1080, position: 3);
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 360), bounds);

        if (switchDuringSample)
            client.Handle.Reading = name =>
            {
                if (name == "video-out-params/dw") client.SetOutput(42, 1080, 1920, position: 3);
            };
        else
            client.SetOutput(42, 1080, 1920, position: 3);
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 360), bounds);
        client.Handle.Reading = null;
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 360, 640), bounds);

        // Reordering the same entry must preserve its identity, even if "current" differs.
        client.Handle.Properties.Remove("playlist/3/id");
        client.Handle.Properties["playlist-current-pos"] = 0;
        client.SetOutput(41, 1920, 1080, position: 0);
        client.SetOutput(42, 1440, 1080, position: 2);
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 480), bounds);
    }

    [Theory]
    [InlineData("playlist-playing-pos")]
    [InlineData("playlist/3/id")]
    public void MissingPlayingIdentityWaitsForValidLoadedEntry(string missingProperty)
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = new CompactOverlayRatioSession(new(() => null, _ => { }), () => bounds,
            () => new(0, 0, 1920, 1080), value => bounds = value);
        using var source = new MpvMediaDisplaySource(client, queue, session.BeginMedia, session.UpdateMedia);
        session.SetCompact(true);
        source.Resume();
        client.SetOutput(41, 1920, 1080, position: 3);
        client.Handle.Properties.Remove(missingProperty);
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 400), bounds);
        client.SetOutput(41, 1920, 1080, position: 3);
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 360), bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdentityChangingDuringLoadedEventRequiresFreshStableLoad(bool replaceEntry)
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = new CompactOverlayRatioSession(new(() => null, _ => { }), () => bounds,
            () => new(0, 0, 1920, 1080), value => bounds = value);
        using var source = new MpvMediaDisplaySource(client, queue, session.BeginMedia, session.UpdateMedia);
        session.SetCompact(true);
        source.Resume();
        client.SetOutput(41, 1920, 1080, position: 3);
        client.Loading();
        var positionReads = 0;
        client.Handle.Reading = name =>
        {
            if (!replaceEntry && name == "playlist/3/id") client.Handle.Properties["playlist-playing-pos"] = -1;
            if (replaceEntry && name == "playlist-playing-pos" && ++positionReads == 2)
                client.SetOutput(42, 1080, 1920, position: 3);
        };
        client.Loaded();
        client.Handle.Reading = null;
        client.SetOutput(41, 1920, 1080, position: 3);
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 400), bounds);
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 360), bounds);
    }

    [Fact]
    public void OutputAvailabilityAndReconfigurationAdaptWhilePausedAndIgnoreTornSamples()
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = new CompactOverlayRatioSession(new(() => null, _ => { }), () => bounds,
            () => new(0, 0, 1920, 1080), value => bounds = value);
        using var source = new MpvMediaDisplaySource(client, queue, session.BeginMedia, session.UpdateMedia);
        session.SetCompact(true);
        source.Resume();
        client.SetOutput(1, 0, 0);
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 400), bounds);
        client.SetOutput(1, 1440, 1080);
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 480), bounds);
        var reads = 0;
        client.Handle.Reading = name =>
        {
            if (name == "video-out-params/dh" && ++reads == 1) client.SetOutput(1, 1080, 1920);
        };
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 480), bounds);
        client.Handle.Reading = null;
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 360, 640), bounds);
        bounds = new(100, 80, 500, 500);
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 500), bounds);
        client.Loading();
        source.Dispose();
        queue.Drain();
        client.SetOutput(2, 1920, 1080);
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 500), bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OldLoadedEventAfterSwitchCannotResizeNewMediaWindow(bool identityUnavailableAtSuspend)
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = new CompactOverlayRatioSession(new(() => null, _ => { }), () => bounds,
            () => new(0, 0, 1920, 1080), value => bounds = value);
        using var source = new MpvMediaDisplaySource(client, queue, session.BeginMedia, session.UpdateMedia);
        session.SetCompact(true);
        source.Resume();
        client.SetOutput(1, 1920, 1080);
        client.Loading();
        client.Loaded();
        queue.Drain();
        queue.MediaTimer.Fire();
        Assert.Equal(360, bounds.Height);
        if (identityUnavailableAtSuspend) client.Handle.Properties["playlist-playing-pos"] = -1;
        source.Suspend();
        client.Handle.Properties["playlist-playing-pos"] = -1; // stop has completed before InitializeCore suspends again
        source.Suspend();
        client.SetOutput(1, 1920, 1080);
        bounds = new(100, 80, 500, 500);
        source.Resume();
        client.Loaded(); // old file notification delivered after the application changed media
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 500), bounds);
        client.SetOutput(2, 1920, 1080, 90);
        client.Loading();
        client.Loaded();
        queue.Drain();
        queue.MediaTimer.Fire();
        Assert.Equal(new RatioWindowBounds(100, 80, 281, 500), bounds);
    }
}
