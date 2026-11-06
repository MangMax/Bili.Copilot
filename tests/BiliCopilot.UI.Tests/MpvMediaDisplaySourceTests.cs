using BiliCopilot.UI.Services;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Dispatching;
using Richasy.MpvKernel.Core;

namespace BiliCopilot.UI.Tests;

public sealed class MpvMediaDisplaySourceTests
{
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

    [Fact]
    public void OldLoadedEventAfterSwitchCannotResizeNewMediaWindow()
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
        source.Suspend();
        client.Handle.Properties.Remove("playlist-entry-id"); // stop has completed before InitializeCore suspends again
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
