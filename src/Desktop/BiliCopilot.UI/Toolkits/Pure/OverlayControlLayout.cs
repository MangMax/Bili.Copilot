// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 播放控件区的两种排布：底部控制带与竖屏右侧轨道。
/// </summary>
internal enum OverlayControlLayoutMode
{
    /// <summary>普通窗口、全屏与横屏小窗使用的底部控制带。</summary>
    Band,

    /// <summary>竖屏小窗使用的右侧纵向轨道。</summary>
    PortraitRail,
}

/// <summary>
/// 底部控制带的宽度档位（横屏小窗与普通窗口沿用原有阈值）。
/// </summary>
internal enum OverlayBandState
{
    /// <summary>窄宽度：收起部分按钮并提供“更多”入口。</summary>
    Narrow,

    /// <summary>中等宽度：隐藏左侧信息卡。</summary>
    Medium,

    /// <summary>低于宽屏：音量改为精简按钮。</summary>
    BelowWide,

    /// <summary>宽屏：完整底部带。</summary>
    Wide,
}

/// <summary>
/// 播放控件区的纯几何决策：只看真实窗口模式与实际可用区域，不依赖媒体比例开关。
/// </summary>
internal static class OverlayControlLayout
{
    /// <summary>底部控制带高度（与原来的 160 一致）。</summary>
    public const double BandHeight = 160d;

    /// <summary>竖屏轨道从窗口右边界算起的交互宽度（轨道本身约 60，留一点余量）。</summary>
    public const double RailWidth = 64d;

    /// <summary>
    /// 只有小窗模式（CompactOverlay presenter）且实际可用区域确为纵向时才使用右侧轨道。
    /// 普通窗口即使被拉成纵向、自动比例关闭后的横向小窗、正方形窗口都不会切换。
    /// </summary>
    public static OverlayControlLayoutMode SelectMode(bool isCompactOverlay, double width, double height)
    {
        if (!isCompactOverlay
            || !double.IsFinite(width)
            || !double.IsFinite(height)
            || width <= 0
            || height <= 0)
        {
            return OverlayControlLayoutMode.Band;
        }

        return height > width ? OverlayControlLayoutMode.PortraitRail : OverlayControlLayoutMode.Band;
    }

    /// <summary>
    /// 底部带的窄宽度阈值：弹幕按钮占用更多横向空间时提前收紧。
    /// </summary>
    public static double NarrowWidth(bool isDanmakuVisible)
        => isDanmakuVisible ? 900d : 760d;

    /// <summary>
    /// 非轨道状态下按窗口宽度选择底部带档位（与改动前的阈值完全一致）。
    /// </summary>
    public static OverlayBandState SelectBandState(double width, bool isDanmakuVisible)
    {
        if (width < NarrowWidth(isDanmakuVisible))
        {
            return OverlayBandState.Narrow;
        }

        if (width < 1100d)
        {
            return OverlayBandState.Medium;
        }

        return width < 1280d ? OverlayBandState.BelowWide : OverlayBandState.Wide;
    }

    /// <summary>
    /// 指针是否落在会唤出/保留控件的区域。轨道模式下右侧轨道与底部带都算，
    /// 底部带保留“移到底部唤出控件”的既有习惯。
    /// </summary>
    public static bool IsInsideInteractiveArea(
        OverlayControlLayoutMode mode,
        double width,
        double height,
        double x,
        double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return false;
        }

        if (y >= height - BandHeight)
        {
            return true;
        }

        return mode == OverlayControlLayoutMode.PortraitRail && x >= width - RailWidth;
    }
}
