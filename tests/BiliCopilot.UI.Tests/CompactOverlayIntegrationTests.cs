using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class CompactOverlayIntegrationTests
{
    [Theory]
    [InlineData(true, 360, 640)]
    [InlineData(false, 640, 400)]
    public async Task PositionRestoresBeforeRatioAndPersistsWithEitherSetting(bool enabled, int width, int height)
    {
        var desktop = new Desktop();
        var placement = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        using var ratio = new CompactOverlayRatioSession(new(() => enabled, _ => { }),
            () => new(desktop.Bounds.X, desktop.Bounds.Y, desktop.Bounds.Width, desktop.Bounds.Height),
            () => new(-1920, 0, 1920, 1080),
            bounds =>
            {
                desktop.Bounds = new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                placement.GeometryChanged();
            });
        ratio.UpdateMedia(ratio.BeginMedia(), 9, 16);
        await ratio.ChangePresenterAsync(
            () => placement.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { }), () => true);
        Assert.Equal(new PlacementBounds(-1000, 100, width, height), desktop.Bounds);
        placement.Close();
        Assert.Equal(new PlacementPoint(-1000, 100), desktop.Saved);
    }

    [Fact]
    public async Task PortraitEntryRestoresTheFlushEdgePositionWithTheFittedSize()
    {
        // 9:16 portrait is 360 wide; the remembered position hugs the right edge (-1920..0).
        var desktop = new Desktop { Bounds = new(100, 100, 640, 400), Saved = new(-360, 400) };
        var placement = new PlayerWindowPlacementSession(desktop, () => Task.CompletedTask);
        using var ratio = new CompactOverlayRatioSession(new(() => true, _ => { }),
            () => new(desktop.Bounds.X, desktop.Bounds.Y, desktop.Bounds.Width, desktop.Bounds.Height),
            () => new(-1920, 0, 1920, 1080),
            bounds => desktop.Bounds = new(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        ratio.UpdateMedia(ratio.BeginMedia(), 9, 16);
        await ratio.ChangePresenterAsync(
            () => placement.ChangeAsync(WindowPlacementMode.CompactOverlay, () => { }), () => true);
        Assert.Equal(new PlacementBounds(-360, 400, 360, 640), desktop.Bounds);
    }

    private sealed class Desktop : IPlayerWindowPlacement
    {
        public PlacementBounds Bounds { get; set; } = new(100, 100, 640, 400);
        public PlacementPoint Saved { get; set; } = new(-1000, 100);
        public PlacementPoint? ReadPosition() => Saved;
        public void WritePosition(PlacementPoint point) => Saved = point;
        public PlacementBounds GetWorkArea(PlacementPoint point) => new(-1920, 0, 1920, 1080);
        public void Move(PlacementPoint point) => Bounds = Bounds with { X = point.X, Y = point.Y };
    }
}
