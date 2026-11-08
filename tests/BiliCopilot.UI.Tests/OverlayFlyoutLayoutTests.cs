using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class OverlayFlyoutLayoutTests
{
    private static readonly double[] PreferredWidths =
    [
        OverlayFlyoutLayout.ChapterPreferredWidth,
        OverlayFlyoutLayout.VolumePreferredWidth,
        OverlayFlyoutLayout.SpeedPreferredWidth,
        OverlayFlyoutLayout.DanmakuPreferredWidth,
    ];

    [Theory]
    [InlineData(270, 480)]
    [InlineData(360, 640)]
    [InlineData(550, 949)]
    public void FlyoutContentFitsLeftOfThePortraitRail(double width, double height)
    {
        foreach (var preferred in PreferredWidths)
        {
            var content = OverlayFlyoutLayout.ContentWidth(preferred, width, true);
            var presenter = content + OverlayFlyoutLayout.PresenterHorizontalPadding + OverlayFlyoutLayout.WindowEdgeGap;
            Assert.True(
                presenter + OverlayControlLayout.RailWidth <= width,
                $"content {content} + presenter {presenter} + rail does not fit {width}x{height}");
        }

        // 字幕弹层覆盖了 FlyoutPresenter 的 Padding，按 0 计算.
        var subtitle = OverlayFlyoutLayout.ContentWidth(
            OverlayFlyoutLayout.SubtitlePreferredWidth, width, true, presenterHorizontalPadding: 0d);
        Assert.True(subtitle + OverlayFlyoutLayout.WindowEdgeGap + OverlayControlLayout.RailWidth <= width);
        Assert.True(OverlayFlyoutLayout.ContentHeight(OverlayFlyoutLayout.SubtitlePreferredHeight, height)
            + OverlayFlyoutLayout.PresenterVerticalPadding + OverlayFlyoutLayout.WindowEdgeGap <= height);
    }

    [Theory]
    [InlineData(720)]
    [InlineData(1280)]
    public void WideOrTallWindowsKeepTheDesignedContentSize(double available)
    {
        Assert.Equal(OverlayFlyoutLayout.ChapterPreferredWidth, OverlayFlyoutLayout.ContentWidth(OverlayFlyoutLayout.ChapterPreferredWidth, available, true));
        Assert.Equal(OverlayFlyoutLayout.VolumePreferredWidth, OverlayFlyoutLayout.ContentWidth(OverlayFlyoutLayout.VolumePreferredWidth, available, true));
        Assert.Equal(OverlayFlyoutLayout.SpeedPreferredWidth, OverlayFlyoutLayout.ContentWidth(OverlayFlyoutLayout.SpeedPreferredWidth, available, false));
        Assert.Equal(OverlayFlyoutLayout.SubtitlePreferredWidth, OverlayFlyoutLayout.ContentWidth(OverlayFlyoutLayout.SubtitlePreferredWidth, available, false, presenterHorizontalPadding: 0d));
        Assert.Equal(OverlayFlyoutLayout.SubtitlePreferredHeight, OverlayFlyoutLayout.ContentHeight(OverlayFlyoutLayout.SubtitlePreferredHeight, available));
    }

    [Fact]
    public void NarrowBandWindowsClampTheContentWidth()
    {
        // 480 宽的普通窄窗：350 的倍速面板仍放得下（480 - 32 - 4 = 444）.
        Assert.Equal(350d, OverlayFlyoutLayout.ContentWidth(OverlayFlyoutLayout.SpeedPreferredWidth, 480, false));
        // 360 宽的窗口：可用 324，设计宽度 350 被收窄.
        Assert.Equal(324d, OverlayFlyoutLayout.ContentWidth(OverlayFlyoutLayout.SpeedPreferredWidth, 360, false));

        // 270 宽的竖屏小窗：给右侧轨道（播放键 52 + 留白 8）让位，
        // 内容收窄到 270 - 32 - 4 - 60 = 174.
        Assert.Equal(174d, OverlayFlyoutLayout.ContentWidth(OverlayFlyoutLayout.SpeedPreferredWidth, 270, true));
    }

    [Fact]
    public void SmallWindowsStillGetPositiveContentSizes()
    {
        Assert.True(OverlayFlyoutLayout.ContentWidth(OverlayFlyoutLayout.SpeedPreferredWidth, 1, true) >= 1d);
        Assert.True(OverlayFlyoutLayout.ContentHeight(OverlayFlyoutLayout.SubtitlePreferredHeight, 1) >= 1d);
        Assert.True(OverlayFlyoutLayout.ScrollHeight(OverlayFlyoutLayout.ChapterListPreferredHeight, 1, OverlayFlyoutLayout.ChapterHeaderHeight) >= 1d);
    }

    [Theory]
    [InlineData(1280, 344)]
    [InlineData(480, 344)]
    [InlineData(360, 296)]
    [InlineData(200, 136)]
    public void ChapterScrollReservesTheHeaderHeight(double availableHeight, double expected)
    {
        Assert.Equal(expected, OverlayFlyoutLayout.ScrollHeight(
            OverlayFlyoutLayout.ChapterListPreferredHeight,
            availableHeight,
            OverlayFlyoutLayout.ChapterHeaderHeight));
    }

    [Fact]
    public void PortraitVolumeAndSpeedUseTheirOwnDesignedWidths()
    {
        // 360 宽的竖屏小窗有 360 - 32 - 4 - 60 = 264 的可用内容宽度：
        // 音量保持窄面板 120，倍速使用 220，而不是把 350 的横向面板缩窄.
        Assert.Equal(120d, OverlayFlyoutLayout.ContentWidth(
            OverlayFlyoutLayout.PortraitVolumePreferredWidth, 360, true));
        Assert.Equal(220d, OverlayFlyoutLayout.ContentWidth(
            OverlayFlyoutLayout.PortraitSpeedPreferredWidth, 360, true));

        // 270 宽时两者都收在 174 内，且仍为正数.
        Assert.Equal(120d, OverlayFlyoutLayout.ContentWidth(
            OverlayFlyoutLayout.PortraitVolumePreferredWidth, 270, true));
        Assert.Equal(174d, OverlayFlyoutLayout.ContentWidth(
            OverlayFlyoutLayout.PortraitSpeedPreferredWidth, 270, true));
    }

    [Fact]
    public void PortraitSpeedQuickItemsAreOneVerticalColumn()
    {
        Assert.Equal(1, OverlayFlyoutLayout.SpeedQuickColumns(true));
        Assert.Equal(6, OverlayFlyoutLayout.SpeedQuickColumns(false));
        Assert.Equal(OverlayFlyoutLayout.BandSpeedQuickColumns, OverlayFlyoutLayout.SpeedQuickColumns(false));
    }

    [Fact]
    public void PortraitVolumeAndSpeedScrollersStayInsideThePortraitWindow()
    {
        foreach (var height in new[] { 330d, 480d, 640d })
        {
            var volume = OverlayFlyoutLayout.ContentHeight(
                OverlayFlyoutLayout.PortraitVolumePreferredHeight, height);
            var speed = OverlayFlyoutLayout.ContentHeight(
                OverlayFlyoutLayout.PortraitSpeedPreferredHeight, height);
            Assert.InRange(volume, 1d, height - OverlayFlyoutLayout.PresenterVerticalPadding);
            Assert.InRange(speed, 1d, height - OverlayFlyoutLayout.PresenterVerticalPadding);
            Assert.True(OverlayFlyoutLayout.PortraitSliderHeight < speed);
        }
    }

    [Fact]
    public void DanmakuPanelFitsThePortraitWindow()
    {
        const double width = 270d;
        const double height = 480d;
        var scroller = OverlayFlyoutLayout.ContentWidth(OverlayFlyoutLayout.DanmakuPreferredWidth, width, true);
        Assert.InRange(scroller, 1d, width - OverlayControlLayout.RailWidth);
        Assert.InRange(
            OverlayFlyoutLayout.ContentHeight(OverlayFlyoutLayout.DanmakuPreferredHeight, height),
            1d,
            height - OverlayFlyoutLayout.PresenterVerticalPadding);
    }
}
