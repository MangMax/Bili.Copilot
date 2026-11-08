// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 弹层内容在真实窗口内的可用尺寸。
/// FlyoutPresenter 使用 WinUI 的 DefaultFlyoutPresenterStyle：Padding 16,15,16,17、
/// MinWidth 96；内容一旦比窗口宽/高，即使用 ShouldConstrainToRootBounds 也只会被裁剪，
/// 所以内容尺寸必须自己收在可用范围内。
/// </summary>
internal static class OverlayFlyoutLayout
{
    /// <summary>DefaultFlyoutPresenterStyle 的左右内边距合计（16 + 16）。</summary>
    public const double PresenterHorizontalPadding = 32d;

    /// <summary>DefaultFlyoutPresenterStyle 的上下内边距合计（15 + 17）。</summary>
    public const double PresenterVerticalPadding = 32d;

    /// <summary>弹层与窗口边缘留出的空隙。</summary>
    public const double WindowEdgeGap = 4d;

    /// <summary>章节弹层的设计宽度（原来的 MinWidth 260）。</summary>
    public const double ChapterPreferredWidth = 260d;

    /// <summary>章节弹层标题行高度：列表滚动区的最大高度要扣掉它。</summary>
    public const double ChapterHeaderHeight = 28d;

    /// <summary>章节列表滚动区的设计最大高度。</summary>
    public const double ChapterListPreferredHeight = 344d;

    /// <summary>音量弹层的设计宽度（原来的固定宽度 200）。</summary>
    public const double VolumePreferredWidth = 200d;

    /// <summary>倍速弹层的设计宽度（原来的 Slider 固定宽度 350）。</summary>
    public const double SpeedPreferredWidth = 350d;

    /// <summary>竖屏音量弹层的设计宽度：纵向滑块不需要原来的 200。</summary>
    public const double PortraitVolumePreferredWidth = 120d;

    /// <summary>竖屏倍速弹层的设计宽度；270 宽的窗口会再收到实际可用宽度。</summary>
    public const double PortraitSpeedPreferredWidth = 220d;

    /// <summary>竖屏音量弹层的设计最大高度（标题 + 纵滑条 + 静音入口）。</summary>
    public const double PortraitVolumePreferredHeight = 280d;

    /// <summary>竖屏倍速弹层的设计最大高度（纵滑条 + 六个纵排快捷项）。</summary>
    public const double PortraitSpeedPreferredHeight = 430d;

    /// <summary>竖屏滑块的高度（音量与倍速共用）。</summary>
    public const double PortraitSliderHeight = 180d;

    /// <summary>竖屏滑块的宽度；横向 State 恢复为 Stretch。</summary>
    public const double PortraitSliderWidth = 40d;

    /// <summary>竖屏倍速快捷项的列数：一列纵排，不再六个挤在横排。</summary>
    public const int PortraitSpeedQuickColumns = 1;

    /// <summary>底部带的倍速快捷项列数（保持原来的六等分横排）。</summary>
    public const int BandSpeedQuickColumns = 6;

    /// <summary>字幕弹层的设计宽度（原来的根 Grid 固定宽度 280）。</summary>
    public const double SubtitlePreferredWidth = 280d;

    /// <summary>字幕弹层的设计最大高度（原来两层列表各自 MaxHeight 400 + 标题）。</summary>
    public const double SubtitlePreferredHeight = 520d;

    /// <summary>弹幕设置弹层的设计宽度。</summary>
    public const double DanmakuPreferredWidth = 360d;

    /// <summary>弹幕设置弹层的设计最大高度。</summary>
    public const double DanmakuPreferredHeight = 520d;

    /// <summary>
    /// 内容可用的宽度：窗口宽度减去 FlyoutPresenter 的内边距与边缘空隙；
    /// 竖屏轨道弹层还要留出右侧轨道本身，保证 Placement=Left 的弹层不用翻转就能放下。
    /// 字幕弹层自己把 FlyoutPresenter 的 Padding 覆盖成 0，用 <paramref name="presenterHorizontalPadding"/> 传 0.
    /// </summary>
    public static double AvailableContentWidth(
        double availableWidth,
        bool reserveRail,
        double presenterHorizontalPadding = PresenterHorizontalPadding)
        => Math.Max(
            1d,
            availableWidth
            - presenterHorizontalPadding
            - WindowEdgeGap
            - (reserveRail ? OverlayControlLayout.RailWidth : 0d));

    /// <summary>内容可用的高度：窗口高度减去 FlyoutPresenter 的上下内边距与边缘空隙。</summary>
    public static double AvailableContentHeight(double availableHeight)
        => Math.Max(1d, availableHeight - PresenterVerticalPadding - WindowEdgeGap);

    /// <summary>倍速快捷项列数：竖屏一列纵排，底部带保持六列横排。</summary>
    public static int SpeedQuickColumns(bool isPortraitRail)
        => isPortraitRail ? PortraitSpeedQuickColumns : BandSpeedQuickColumns;

    /// <summary>按设计宽度取内容宽度，但不超过实际可用宽度。</summary>
    public static double ContentWidth(
        double preferredWidth,
        double availableWidth,
        bool reserveRail,
        double presenterHorizontalPadding = PresenterHorizontalPadding)
        => Math.Max(
            1d,
            Math.Min(
                preferredWidth,
                AvailableContentWidth(availableWidth, reserveRail, presenterHorizontalPadding)));

    /// <summary>按设计高度取内容高度，但不超过实际可用高度。</summary>
    public static double ContentHeight(double preferredHeight, double availableHeight)
        => Math.Max(1d, Math.Min(preferredHeight, AvailableContentHeight(availableHeight)));

    /// <summary>
    /// 滚动区高度：除弹层上下内边距外，还要扣掉同一弹层里不滚动的标题行高度，
    /// 保证滚动条永远能滚到最后一个条目，而不是被外层裁剪。
    /// </summary>
    public static double ScrollHeight(double preferredHeight, double availableHeight, double fixedChromeHeight)
        => Math.Max(1d, Math.Min(preferredHeight, AvailableContentHeight(availableHeight) - Math.Max(0d, fixedChromeHeight)));
}
