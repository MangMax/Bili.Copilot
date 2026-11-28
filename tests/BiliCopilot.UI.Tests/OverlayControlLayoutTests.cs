using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class OverlayControlLayoutTests
{
    [Theory]
    [InlineData(false, 1280, 720, OverlayControlMode.Fill)]
    [InlineData(false, 270, 500, OverlayControlMode.Fill)]
    [InlineData(true, 640, 360, OverlayControlMode.CompactLandscape)]
    [InlineData(true, 360, 360, OverlayControlMode.CompactLandscape)]
    [InlineData(true, 270, 500, OverlayControlMode.CompactPortrait)]
    [InlineData(true, 0, 500, OverlayControlMode.CompactLandscape)]
    [InlineData(true, double.NaN, 500, OverlayControlMode.CompactLandscape)]
    public void WindowModeAndClientShapeSelectPresentation(bool compact, double width, double height, object expected)
        => Assert.Equal((OverlayControlMode)expected, ResolveMode(compact, width, height));

    [Theory]
    [InlineData(OverlayWindowMode.Overlapped, true, true, OverlayControlMode.Default, true)]
    [InlineData(OverlayWindowMode.Overlapped, true, false, OverlayControlMode.Fill, true)]
    [InlineData(OverlayWindowMode.Overlapped, false, true, OverlayControlMode.Fill, false)]
    [InlineData(OverlayWindowMode.FullScreen, true, true, OverlayControlMode.FullScreen, false)]
    [InlineData(OverlayWindowMode.Compact, true, true, OverlayControlMode.CompactPortrait, false)]
    public void PresenterAndPreferenceSelectOneLayout(object window, bool supportsDetails, bool prefersDetails, object mode, bool canToggle)
    {
        var presentation = OverlayControlLayout.Resolve((OverlayWindowMode)window, supportsDetails, prefersDetails, 400, 600);
        Assert.Equal((OverlayControlMode)mode, presentation.Mode);
        Assert.Equal(canToggle, presentation.CanToggleLayout);
    }

    [Theory]
    [InlineData(700, "DefaultNarrowState", false)]
    [InlineData(900, "DefaultNarrowState", true)]
    [InlineData(600, "DefaultMinimumWidthState", false)]
    [InlineData(1200, "DefaultWideState", true)]
    public void DefaultLayoutUsesItsOwnWidthProfile(double width, string state, bool showsTime)
    {
        var mode = OverlayControlLayout.Resolve(OverlayWindowMode.Overlapped, true, true, width, 900).Mode;
        Assert.Equal(state, OverlayControlLayout.SelectVisualStates(mode, width, true).Controls);
        Assert.Equal(showsTime, OverlayControlLayout.ShowsDetailsTime(mode, width));
    }

    [Theory]
    [InlineData(1200, 900, 0)]
    [InlineData(1300, 900, 450)]
    [InlineData(1030, 700, 410)]
    [InlineData(1010, 700, 0)]
    public void SidebarFitsTheDefaultLayout(double width, double height, double expected)
    {
        var presentation = OverlayControlLayout.Resolve(OverlayWindowMode.Overlapped, true, true, width, height);
        Assert.Equal(expected, OverlayControlLayout.GetSidebarWidth(presentation, width, height, 400, 450));
    }

    [Fact]
    public void LeavingPortraitRestoresTheNormalWidthProfile()
    {
        Assert.Equal(("CompactPortraitLayout", "CompactPortraitState"), OverlayControlLayout.SelectVisualStates(ResolveMode(true, 360, 640), 360, true));
        Assert.Equal(("OverlayLayout", "WideState"), OverlayControlLayout.SelectVisualStates(ResolveMode(false, 1600, 900), 1600, true));
        Assert.Equal(("CompactControlsLayout", "NarrowState"), OverlayControlLayout.SelectVisualStates(ResolveMode(true, 640, 360), 640, true));
        Assert.Equal(("CompactControlsLayout", "NarrowState"), OverlayControlLayout.SelectVisualStates(ResolveMode(false, 640, 360), 640, true));
    }

    [Theory]
    [InlineData(OverlayControlMode.CompactPortrait, 255, 200, true)]
    [InlineData(OverlayControlMode.CompactLandscape, 255, 200, false)]
    [InlineData(OverlayControlMode.Fill, 255, 200, false)]
    [InlineData(OverlayControlMode.CompactPortrait, 120, 450, true)]
    [InlineData(OverlayControlMode.Fill, 120, 450, true)]
    [InlineData(OverlayControlMode.CompactPortrait, 120, 200, false)]
    [InlineData(OverlayControlMode.CompactPortrait, -1, 450, false)]
    [InlineData(OverlayControlMode.CompactPortrait, 280, 450, false)]
    public void OnlyVisibleControlRegionsKeepTheOverlayAwake(object mode, double x, double y, bool expected)
        => Assert.Equal(expected, OverlayControlLayout.Contains((OverlayControlMode)mode, 270, 500, x, y));

    private static OverlayControlMode ResolveMode(bool compact, double width, double height)
        => OverlayControlLayout.Resolve(compact ? OverlayWindowMode.Compact : OverlayWindowMode.Overlapped,
            true, false, width, height).Mode;
}
