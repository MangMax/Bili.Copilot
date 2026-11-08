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
        using var window = await RatioWindowHarness.CreateCompactAsync();
        using var source = new MpvMediaDisplaySource(client, queue, window.Ratio.BeginMedia, window.Ratio.UpdateMedia);
        source.Suspend();
        var publish = source.CreateMetadataPublisher();
        source.Resume();
        var pending = publish(1080, 1920);
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 360, 640), window.Bounds);
        client.Loading();
        queue.Drain();
        window.Bounds = new(100, 80, 500, 500); // a manual resize during load
        client.SetOutput(1, 1080, 1920);
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 500, 500), window.Bounds);
        pending = publish(1920, 1080); // equal-ratio runtime output still owns priority
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 500, 500), window.Bounds);
        client.SetOutput(1, 1440, 1080);
        queue.MediaTimer.Fire();
        // 4:3 output belongs to the landscape form (16:9), not its own ratio.
        Assert.Equal(new WindowRect(100, 80, 500, 281), window.Bounds);
        pending = publish(1080, 1920);
        queue.Drain();
        await pending;
        // The runtime output already owns the window, so this later hint must be ignored.
        Assert.Equal(new WindowRect(100, 80, 500, 281), window.Bounds);
    }

    [Fact]
    public async Task SwitchRejectsQueuedAndLateMetadataAndResetsRuntimePriority()
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        using var window = await RatioWindowHarness.CreateCompactAsync();
        using var source = new MpvMediaDisplaySource(client, queue, window.Ratio.BeginMedia, window.Ratio.UpdateMedia);
        var old = source.CreateMetadataPublisher();
        source.Resume();
        client.SetOutput(1, 1920, 1080);
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        var queued = old(1000, 1000);
        source.Suspend();
        window.Bounds = new(100, 80, 500, 500);
        source.Resume();
        var current = source.CreateMetadataPublisher();
        queue.Drain();
        await queued;
        Assert.Equal(new WindowRect(100, 80, 500, 500), window.Bounds);
        var pending = current(1080, 1920);
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 281, 500), window.Bounds);
        pending = old(1920, 1080);
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 281, 500), window.Bounds);
        client.End();
        pending = current(1000, 1000);
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 281, 500), window.Bounds);
        source.Dispose();
        pending = current(1920, 1080);
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 281, 500), window.Bounds);
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
        using var window = new RatioWindowHarness(enabled: false);
        await window.Presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        using var source = new MpvMediaDisplaySource(client, queue, window.Ratio.BeginMedia, window.Ratio.UpdateMedia);
        source.Resume();
        var publish = source.CreateMetadataPublisher();
        window.Preference.SetEnabled(true);
        var pending = publish(width, height);
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 640, 400), window.Bounds);
        window.Preference.SetEnabled(false);
        pending = publish(1080, 1920);
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 640, 400), window.Bounds);
        window.Preference.SetEnabled(true);
        Assert.Equal(new WindowRect(100, 80, 360, 640), window.Bounds);
        window.Bounds = new(100, 80, 500, 500);
        pending = publish(2160, 3840);
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 500, 500), window.Bounds);
        client.Loading();
        client.SetOutput(1, 0, 0);
        client.Loaded();
        queue.MediaTimer.Fire();
        pending = publish(1440, 1080); // invalid runtime output must not suppress usable hints
        queue.Drain();
        await pending;
        Assert.Equal(new WindowRect(100, 80, 500, 281), window.Bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReusedPlaylistSlotCannotPublishUntilNewEntryLoads(bool switchDuringSample)
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        using var window = await RatioWindowHarness.CreateCompactAsync();
        using var source = new MpvMediaDisplaySource(client, queue, window.Ratio.BeginMedia, window.Ratio.UpdateMedia);
        source.Resume();
        client.SetOutput(41, 1920, 1080, position: 3);
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 640, 360), window.Bounds);

        if (switchDuringSample)
            client.Handle.Reading = name =>
            {
                if (name == "video-out-params/dw") client.SetOutput(42, 1080, 1920, position: 3);
            };
        else
            client.SetOutput(42, 1080, 1920, position: 3);
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 640, 360), window.Bounds);
        client.Handle.Reading = null;
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 360, 640), window.Bounds);

        // Reordering the same entry must preserve its identity, even if "current" differs.
        client.Handle.Properties.Remove("playlist/3/id");
        client.Handle.Properties["playlist-current-pos"] = 0;
        client.SetOutput(41, 1920, 1080, position: 0);
        client.SetOutput(42, 1440, 1080, position: 2);
        queue.MediaTimer.Fire();
        // 4:3 is still the landscape form, so the 16:9 bounds do not change again.
        Assert.Equal(new WindowRect(100, 80, 640, 360), window.Bounds);
    }

    [Theory]
    [InlineData("playlist-playing-pos")]
    [InlineData("playlist/3/id")]
    public async Task MissingPlayingIdentityWaitsForValidLoadedEntry(string missingProperty)
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        using var window = await RatioWindowHarness.CreateCompactAsync();
        using var source = new MpvMediaDisplaySource(client, queue, window.Ratio.BeginMedia, window.Ratio.UpdateMedia);
        source.Resume();
        client.SetOutput(41, 1920, 1080, position: 3);
        client.Handle.Properties.Remove(missingProperty);
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 640, 400), window.Bounds);
        client.SetOutput(41, 1920, 1080, position: 3);
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 640, 360), window.Bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdentityChangingDuringLoadedEventRequiresFreshStableLoad(bool replaceEntry)
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        using var window = await RatioWindowHarness.CreateCompactAsync();
        using var source = new MpvMediaDisplaySource(client, queue, window.Ratio.BeginMedia, window.Ratio.UpdateMedia);
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
        Assert.Equal(new WindowRect(100, 80, 640, 400), window.Bounds);
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 640, 360), window.Bounds);
    }

    [Fact]
    public async Task OutputAvailabilityAndReconfigurationAdaptWhilePausedAndIgnoreTornSamples()
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        using var window = await RatioWindowHarness.CreateCompactAsync();
        using var source = new MpvMediaDisplaySource(client, queue, window.Ratio.BeginMedia, window.Ratio.UpdateMedia);
        source.Resume();
        client.SetOutput(1, 0, 0);
        client.Loading();
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 640, 400), window.Bounds);
        client.SetOutput(1, 1440, 1080);
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 640, 360), window.Bounds);
        var reads = 0;
        client.Handle.Reading = name =>
        {
            if (name == "video-out-params/dh" && ++reads == 1) client.SetOutput(1, 1080, 1920);
        };
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 640, 360), window.Bounds);
        client.Handle.Reading = null;
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 360, 640), window.Bounds);
        window.Bounds = new(100, 80, 500, 500);
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 500, 500), window.Bounds);
        client.Loading();
        source.Dispose();
        queue.Drain();
        client.SetOutput(2, 1920, 1080);
        client.Loaded();
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 500, 500), window.Bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OldLoadedEventAfterSwitchCannotResizeNewMediaWindow(bool identityUnavailableAtSuspend)
    {
        var client = new MpvClient();
        var queue = new DispatcherQueue();
        using var window = await RatioWindowHarness.CreateCompactAsync();
        using var source = new MpvMediaDisplaySource(client, queue, window.Ratio.BeginMedia, window.Ratio.UpdateMedia);
        source.Resume();
        client.SetOutput(1, 1920, 1080);
        client.Loading();
        client.Loaded();
        queue.Drain();
        queue.MediaTimer.Fire();
        Assert.Equal(360, window.Bounds.Height);
        if (identityUnavailableAtSuspend) client.Handle.Properties["playlist-playing-pos"] = -1;
        source.Suspend();
        client.Handle.Properties["playlist-playing-pos"] = -1; // stop has completed before InitializeCore suspends again
        source.Suspend();
        client.SetOutput(1, 1920, 1080);
        window.Bounds = new(100, 80, 500, 500);
        source.Resume();
        client.Loaded(); // old file notification delivered after the application changed media
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 500, 500), window.Bounds);
        client.SetOutput(2, 1920, 1080, 90);
        client.Loading();
        client.Loaded();
        queue.Drain();
        queue.MediaTimer.Fire();
        Assert.Equal(new WindowRect(100, 80, 281, 500), window.Bounds);
    }
}