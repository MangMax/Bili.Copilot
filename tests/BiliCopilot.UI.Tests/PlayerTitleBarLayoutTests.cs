using BiliCopilot.UI.Toolkits;
using Xunit;

namespace BiliCopilot.UI.Tests;

public sealed class PlayerTitleBarLayoutTests
{
    [Theory]
    [InlineData(480, false, false)]
    [InlineData(699, false, false)]
    [InlineData(700, false, true)]
    [InlineData(1200, false, true)]
    [InlineData(270, true, false)]
    [InlineData(1200, true, false)]
    public void NarrowAndCompactWindowsCollapseTheEntireActionGroup(double width, bool compact, bool expected)
        => Assert.Equal(expected, PlayerTitleBarLayout.Create(width, 44, 1.5, 0, 207, compact).ShowActions);

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void StaleWideControlBoundsCannotCoverNativeCaptionButtons(double scale)
    {
        var layout = PlayerTitleBarLayout.Create(480, 44, scale, 0, 207, false);
        var clipped = layout.Clip(250, 0, 700, 44);
        Assert.True(clipped.X + clipped.Width <= 480 - 207 / scale);
        Assert.Equal(default, layout.Clip(480 - 207 / scale, 0, 207 / scale, 44));
    }

    [Fact]
    public void PresenterChangesUseTheirOwnCaptionInsets()
    {
        var normal = PlayerTitleBarLayout.Create(480, 44, 1.5, 0, 207, false);
        var compact = PlayerTitleBarLayout.Create(270, 44, 1.5, 0, 69, true);
        var fullScreen = PlayerTitleBarLayout.Create(1280, 44, 1.5, 0, 0, false);
        Assert.Equal(342, normal.Right);
        Assert.Equal(224, compact.Right);
        Assert.Equal(1280, fullScreen.Right);
    }

    [Fact]
    public void HiddenHeaderRevealAreaExcludesBothNativeInsets()
    {
        var layout = PlayerTitleBarLayout.Create(480, 44, 1.5, 60, 207, false);
        Assert.Equal((40d, 0d, 302d, 44d), layout.Clip(0, -20, 1000, 100));
    }

    [Fact]
    public void InvalidOrEmptyGeometryNeverCreatesInputRegions()
    {
        Assert.Equal(default, PlayerTitleBarLayout.Create(double.NaN, 44, 1, 0, 0, false));
        Assert.Equal(default, PlayerTitleBarLayout.Create(480, 44, 0, 0, 0, false));
        var layout = PlayerTitleBarLayout.Create(30, 44, 1, 40, 200, false);
        Assert.Equal(default, layout.Clip(0, 0, 200, 44));
        Assert.Equal(default, layout.Clip(double.NaN, 0, 200, 44));
    }
}
