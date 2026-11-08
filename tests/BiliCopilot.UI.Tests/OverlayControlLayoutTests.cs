using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class OverlayControlLayoutTests
{
    [Theory]
    [InlineData(false, 1280, 720, OverlayControlMode.Normal)]
    [InlineData(false, 270, 500, OverlayControlMode.Normal)]
    [InlineData(true, 640, 360, OverlayControlMode.CompactLandscape)]
    [InlineData(true, 360, 360, OverlayControlMode.CompactLandscape)]
    [InlineData(true, 270, 500, OverlayControlMode.CompactPortrait)]
    [InlineData(true, 0, 500, OverlayControlMode.CompactLandscape)]
    [InlineData(true, double.NaN, 500, OverlayControlMode.CompactLandscape)]
    public void WindowModeAndClientShapeSelectPresentation(bool compact, double width, double height, object expected)
        => Assert.Equal((OverlayControlMode)expected, OverlayControlLayout.SelectMode(compact, width, height));

    [Fact]
    public void LeavingPortraitRestoresTheNormalWidthProfile()
    {
        Assert.Equal("CompactPortraitState", OverlayControlLayout.StateName(OverlayControlLayout.SelectMode(true, 360, 640), 360, true));
        Assert.Equal("WideState", OverlayControlLayout.StateName(OverlayControlLayout.SelectMode(false, 1600, 900), 1600, true));
        Assert.Equal("CompactLandscapeState", OverlayControlLayout.StateName(OverlayControlLayout.SelectMode(true, 640, 360), 640, true));
        Assert.Equal("NarrowState", OverlayControlLayout.StateName(OverlayControlLayout.SelectMode(false, 640, 360), 640, true));
    }

    [Theory]
    [InlineData(OverlayControlMode.CompactPortrait, 255, 200, true)]
    [InlineData(OverlayControlMode.CompactLandscape, 255, 200, false)]
    [InlineData(OverlayControlMode.Normal, 255, 200, false)]
    [InlineData(OverlayControlMode.CompactPortrait, 120, 450, true)]
    [InlineData(OverlayControlMode.Normal, 120, 450, true)]
    [InlineData(OverlayControlMode.CompactPortrait, 120, 200, false)]
    [InlineData(OverlayControlMode.CompactPortrait, -1, 450, false)]
    [InlineData(OverlayControlMode.CompactPortrait, 280, 450, false)]
    public void OnlyVisibleControlRegionsKeepTheOverlayAwake(object mode, double x, double y, bool expected)
        => Assert.Equal(expected, OverlayControlLayout.Contains((OverlayControlMode)mode, 270, 500, x, y));
}
