using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class CompactOverlayRatioTests
{
    [Fact]
    public void PersistedSettingKeyAndLocalizedToggleTextAreAvailable()
    {
        Assert.Equal("AutoCompactOverlayRatio", Models.Constants.SettingNames.AutoCompactOverlayRatio.ToString());
        var resources = ResourcesFileHelper.LoadResources();
        foreach (var key in new[] { "AutoCompactOverlayRatio", "AutoCompactOverlayRatioDescription" })
        {
            var entry = Assert.Single(resources, data => (string?)data.Attribute("name") == key);
            Assert.False(string.IsNullOrWhiteSpace(entry.Element("value")?.Value));
        }
    }

    [Fact]
    public async Task OlderPresenterCompletionCannotAdaptDuringNewTransitionOrAfterClose()
    {
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = CreateSession(() => bounds, value => bounds = value);
        session.UpdateMedia(session.BeginMedia(), 9, 16);
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        var entering = session.ChangePresenterAsync(() => first.Task, () => true);
        var leaving = session.ChangePresenterAsync(() => second.Task, () => false);
        first.SetResult();
        await entering;
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 400), bounds);
        second.SetResult();
        await leaving;
        var last = new TaskCompletionSource();
        var closing = session.ChangePresenterAsync(() => last.Task, () => true);
        session.Dispose();
        last.SetResult();
        await closing;
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 400), bounds);
    }

    [Fact]
    public void PortraitShrinksProportionallyAndKeepsTheAnchorOnANegativeWorkArea()
    {
        var bounds = new RatioWindowBounds(-200, 500, 1000, 600);
        using var session = new CompactOverlayRatioSession(new(() => null, _ => { }), () => bounds,
            () => new(-1280, 0, 1280, 720), value => bounds = value);
        session.UpdateMedia(session.BeginMedia(), 9, 16);
        session.SetCompact(true);
        Assert.Equal(new RatioWindowBounds(-200, 500, 405, 720), bounds);
    }

    [Fact]
    public void FittingKeepsAHalfOffscreenAnchorInsteadOfPullingTheWindowInside()
    {
        var bounds = new RatioWindowBounds(1900, 1000, 640, 400);
        using var session = CreateSession(() => bounds, value => bounds = value);
        session.UpdateMedia(session.BeginMedia(), 9, 16);
        session.SetCompact(true);
        Assert.Equal(new RatioWindowBounds(1900, 1000, 360, 640), bounds);
    }

    [Fact]
    public void FullyHiddenAnchorIsRecoveredWithTheFittedSize()
    {
        var bounds = new RatioWindowBounds(4000, 3000, 640, 400);
        using var session = CreateSession(() => bounds, value => bounds = value);
        session.UpdateMedia(session.BeginMedia(), 9, 16);
        session.SetCompact(true);
        Assert.Equal(new RatioWindowBounds(1560, 440, 360, 640), bounds);
    }

    [Fact]
    public void LeavingAndClosingRejectMediaAndSettingsUntilNextStableEntry()
    {
        var preference = new CompactOverlayRatioPreference(() => null, _ => { });
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        var session = new CompactOverlayRatioSession(preference, () => bounds, () => new(0, 0, 1920, 1080), value => bounds = value);
        var generation = session.BeginMedia();
        session.SetCompact(true);
        session.SetCompact(false);
        session.UpdateMedia(generation, 9, 16);
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 400), bounds);
        session.SetCompact(true);
        Assert.Equal(new RatioWindowBounds(100, 80, 360, 640), bounds);
        session.Dispose();
        session.UpdateMedia(generation, 1, 1);
        session.SetCompact(true);
        preference.SetEnabled(false);
        preference.SetEnabled(true);
        Assert.Equal(new RatioWindowBounds(100, 80, 360, 640), bounds);
    }

    [Fact]
    public void GlobalPreferencePersistsAndReenableAdaptsAllWindowsButRepeatedEnableDoesNot()
    {
        bool? stored = null;
        var preference = new CompactOverlayRatioPreference(() => stored, value => stored = value);
        var first = new RatioWindowBounds(10, 20, 640, 400);
        var second = new RatioWindowBounds(30, 40, 320, 200);
        using var a = new CompactOverlayRatioSession(preference, () => first, () => new(0, 0, 1920, 1080), value => first = value);
        using var b = new CompactOverlayRatioSession(preference, () => second, () => new(0, 0, 1920, 1080), value => second = value);
        a.SetCompact(true);
        b.SetCompact(true);
        preference.SetEnabled(false);
        a.UpdateMedia(a.BeginMedia(), 9, 16);
        b.UpdateMedia(b.BeginMedia(), 9, 16);
        Assert.Equal(640, first.Width);
        Assert.Equal(320, second.Width);
        Assert.False(new CompactOverlayRatioPreference(() => stored, _ => { }).Enabled);
        preference.SetEnabled(true);
        Assert.Equal(new RatioWindowBounds(10, 20, 360, 640), first);
        Assert.Equal(new RatioWindowBounds(30, 40, 180, 320), second);
        first = new(10, 20, 500, 500);
        preference.SetEnabled(true);
        Assert.Equal(new RatioWindowBounds(10, 20, 500, 500), first);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(-1, 1080)]
    [InlineData(1920, 0)]
    [InlineData(double.NaN, 1080)]
    [InlineData(double.PositiveInfinity, 1080)]
    [InlineData(1920, double.PositiveInfinity)]
    public void InvalidDimensionsWaitWithoutLosingLastValidRatio(double width, double height)
    {
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = CreateSession(() => bounds, value => bounds = value);
        var generation = session.BeginMedia();
        session.UpdateMedia(generation, 1920, 1080);
        session.UpdateMedia(generation, width, height);
        session.SetCompact(true);
        Assert.Equal(new RatioWindowBounds(100, 80, 640, 360), bounds);
    }

    [Fact]
    public void SwitchingMediaRejectsOldCallbacksAndDuplicateRatioPreservesManualResize()
    {
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        using var session = CreateSession(() => bounds, value => bounds = value);
        session.SetCompact(true);
        var old = session.BeginMedia();
        session.UpdateMedia(old, 1920, 1080);
        var current = session.BeginMedia();
        session.UpdateMedia(current, 1080, 1920);
        session.UpdateMedia(old, 1920, 1080);
        Assert.Equal(new RatioWindowBounds(100, 80, 360, 640), bounds);
        bounds = new(100, 80, 500, 500);
        session.UpdateMedia(current, 2160, 3840);
        Assert.Equal(new RatioWindowBounds(100, 80, 500, 500), bounds);
    }

    private static CompactOverlayRatioSession CreateSession(Func<RatioWindowBounds> read, Action<RatioWindowBounds> apply)
        => new(new(() => null, _ => { }), read, () => new(0, 0, 1920, 1080), apply);

    [Theory]
    [InlineData(1920, 1080, 640, 360)]
    [InlineData(1080, 1920, 360, 640)]
    [InlineData(1000, 1000, 640, 640)]
    [InlineData(2100, 900, 640, 274)]
    public void EnterCompactUsesActualRatioAndPreservesAnchorAndLongEdge(double width, double height, int expectedWidth, int expectedHeight)
    {
        var bounds = new RatioWindowBounds(100, 80, 640, 400);
        var preference = new CompactOverlayRatioPreference(() => null, _ => { });
        using var session = new CompactOverlayRatioSession(preference, () => bounds,
            () => new RatioWindowBounds(0, 0, 1920, 1080), value => bounds = value);
        var generation = session.BeginMedia();
        session.UpdateMedia(generation, width, height);
        session.SetCompact(true);
        Assert.Equal(new RatioWindowBounds(100, 80, expectedWidth, expectedHeight), bounds);
    }
}
