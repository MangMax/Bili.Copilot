using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class CompactOverlayIntegrationTests
{
    [Theory]
    [InlineData(1920, 1080, 640, 360)]
    [InlineData(1080, 1920, 360, 640)]
    public async Task OneCoordinatorRestoresPositionFitsTheKnownFormAndPersistsTheFinalOrigin(
        double mediaWidth, double mediaHeight, int expectedWidth, int expectedHeight)
    {
        var surface = new FakeWindowSurface();
        var state = new FakeWindowStateStore { Compact = new(600, 200) };
        using var ratio = new CompactOverlayRatio(new(() => true, _ => { }));
        var presentation = new PlayerWindowPresentation(surface, state, ratio);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        ratio.UpdateMedia(ratio.BeginMedia(), mediaWidth, mediaHeight);

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);

        Assert.Equal(new WindowRect(600, 200, expectedWidth, expectedHeight), surface.Bounds);
        Assert.Empty(surface.VisibleGeometry);
        Assert.Equal(new WindowPoint(600, 200), state.Compact);

        await presentation.RequestModeAsync(WindowPresentationMode.Ordinary);

        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
        Assert.Equal(new WindowPoint(600, 200), state.Compact);
        Assert.Empty(surface.VisibleGeometry);
    }
}
