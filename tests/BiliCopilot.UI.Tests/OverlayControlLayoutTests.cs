using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class OverlayControlLayoutTests
{
    [Theory]
    [InlineData(360, 640)]
    [InlineData(180, 320)]
    [InlineData(359, 360)]
    public void PortraitCompactOverlayUsesTheRightRail(double width, double height)
        => Assert.Equal(OverlayControlLayoutMode.PortraitRail, OverlayControlLayout.SelectMode(true, width, height));

    [Theory]
    [InlineData(640, 360)]
    [InlineData(360, 360)]
    [InlineData(360, 359)]
    [InlineData(1280, 720)]
    public void LandscapeOrSquareCompactOverlayKeepsTheBottomBand(double width, double height)
        => Assert.Equal(OverlayControlLayoutMode.Band, OverlayControlLayout.SelectMode(true, width, height));

    [Theory]
    [InlineData(360, 640)]
    [InlineData(640, 1080)]
    public void OrdinaryPortraitWindowNeverSwitchesToTheRail(double width, double height)
        => Assert.Equal(OverlayControlLayoutMode.Band, OverlayControlLayout.SelectMode(false, width, height));

    [Theory]
    [InlineData(0, 640)]
    [InlineData(360, 0)]
    [InlineData(-360, 640)]
    [InlineData(double.NaN, 640)]
    [InlineData(360, double.NaN)]
    [InlineData(double.PositiveInfinity, 640)]
    public void UnknownOrUnusableSizesFallBackToTheBottomBand(double width, double height)
        => Assert.Equal(OverlayControlLayoutMode.Band, OverlayControlLayout.SelectMode(true, width, height));

    [Fact]
    public void BandWidthThresholdsMatchThePreviousBehaviour()
    {
        Assert.Equal(OverlayBandState.Narrow, OverlayControlLayout.SelectBandState(759, false));
        Assert.Equal(OverlayBandState.Medium, OverlayControlLayout.SelectBandState(760, false));
        Assert.Equal(OverlayBandState.Medium, OverlayControlLayout.SelectBandState(1099, false));
        Assert.Equal(OverlayBandState.BelowWide, OverlayControlLayout.SelectBandState(1100, false));
        Assert.Equal(OverlayBandState.BelowWide, OverlayControlLayout.SelectBandState(1279, false));
        Assert.Equal(OverlayBandState.Wide, OverlayControlLayout.SelectBandState(1280, false));
        Assert.Equal(OverlayBandState.Narrow, OverlayControlLayout.SelectBandState(899, true));
        Assert.Equal(OverlayBandState.Medium, OverlayControlLayout.SelectBandState(900, true));
        Assert.Equal(OverlayBandState.Wide, OverlayControlLayout.SelectBandState(1280, true));
    }

    [Fact]
    public void DanmakuButtonsRaiseTheNarrowThreshold()
    {
        Assert.Equal(760d, OverlayControlLayout.NarrowWidth(false));
        Assert.Equal(900d, OverlayControlLayout.NarrowWidth(true));
        Assert.Equal(OverlayBandState.Narrow, OverlayControlLayout.SelectBandState(800, true));
        Assert.Equal(OverlayBandState.Medium, OverlayControlLayout.SelectBandState(800, false));
    }

    [Theory]
    [InlineData(1920, 1080, false)]
    [InlineData(1080, 1920, true)]
    public void FittedCompactFrameSelectsTheMatchingControlLayout(
        double mediaWidth, double mediaHeight, bool expectRail)
    {
        // 与窗口比例协调器使用同一份几何：横屏小窗 640x360、竖屏小窗 360x640.
        var form = CompactOverlayLayout.SelectForm(mediaWidth / mediaHeight);
        var frame = CompactOverlayLayout.Fit(
            new WindowPoint(100, 100),
            new WindowRect(100, 100, 640, 360),
            0,
            0,
            new WindowRect(0, 0, 1920, 1040),
            form);

        var expected = expectRail ? OverlayControlLayoutMode.PortraitRail : OverlayControlLayoutMode.Band;
        Assert.Equal(expected, OverlayControlLayout.SelectMode(true, frame.Width, frame.Height));
    }

    [Fact]
    public void BottomBandStaysInteractiveInPortraitMode()
    {
        // 轨道模式下鼠标移到底部仍然唤出控件，这是原有习惯.
        Assert.True(OverlayControlLayout.IsInsideInteractiveArea(
            OverlayControlLayoutMode.PortraitRail, 360, 640, 120, 640 - 10));
        Assert.True(OverlayControlLayout.IsInsideInteractiveArea(
            OverlayControlLayoutMode.Band, 360, 640, 120, 640 - 10));
    }

    [Fact]
    public void RightRailIsInteractiveInPortraitModeOnly()
    {
        Assert.True(OverlayControlLayout.IsInsideInteractiveArea(
            OverlayControlLayoutMode.PortraitRail, 360, 640, 360 - 10, 300));
        Assert.False(OverlayControlLayout.IsInsideInteractiveArea(
            OverlayControlLayoutMode.Band, 360, 640, 360 - 10, 300));
    }

    [Fact]
    public void MiddleOfTheVideoDoesNotKeepControlsVisible()
    {
        foreach (var mode in new[] { OverlayControlLayoutMode.Band, OverlayControlLayoutMode.PortraitRail })
        {
            Assert.False(OverlayControlLayout.IsInsideInteractiveArea(mode, 360, 640, 180, 320));
        }
    }

    [Theory]
    [InlineData(double.NaN, 320)]
    [InlineData(180, double.NaN)]
    [InlineData(double.PositiveInfinity, 320)]
    public void InvalidPointerPositionsAreOutside(double x, double y)
        => Assert.False(OverlayControlLayout.IsInsideInteractiveArea(
            OverlayControlLayoutMode.PortraitRail, 360, 640, x, y));

    [Fact]
    public void HitAreasStaySmallerThanThePortraitFormTheyApplyTo()
    {
        // 640 高的竖屏小窗宽约 360：轨道交互宽度必须明显小于窗口宽度，
        // 否则整条视频区都会被当成控件区；底部带高度也必须小于窗口高度.
        var width = 640d * CompactOverlayLayout.PortraitRatio;
        Assert.InRange(OverlayControlLayout.RailWidth, 1d, width / 2d);
        Assert.InRange(OverlayControlLayout.BandHeight, 1d, 640d);
    }
}
