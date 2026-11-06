using BiliCopilot.UI.Services;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Dispatching;
using Richasy.MpvKernel.Core;

namespace BiliCopilot.UI.Tests;

public sealed class MpvMediaDisplaySourceTests
{
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
