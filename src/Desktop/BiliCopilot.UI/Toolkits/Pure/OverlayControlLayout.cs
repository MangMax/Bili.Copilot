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

    /// <summary>轨道普通按钮的边长（与 BasicPlayerIconButtonStyle / TogglePlayerIconButtonStyle 的 40 一致）。</summary>
    public const double RailButtonSize = 40d;

    /// <summary>轨道播放键的边长（PrimaryPlayerButtonStyle 的 52）。</summary>
    public const double RailPrimaryButtonSize = 52d;

    /// <summary>轨道右侧留给窗口边缘的间距（与 PortraitState 里两个面板的 Margin 8 一致）。</summary>
    public const double RailGutter = 8d;

    /// <summary>
    /// 轨道从窗口右边界算起的交互宽度：最宽按钮（播放键 52） + 右侧间距，
    /// 和实际渲染的轨道宽度相同。改按钮样式或轨道 Margin 时必须同步这里，
    /// 否则播放键左缘会落在命中区之外，鼠标停在上面也会被判成离开控制区。
    /// </summary>
    public const double RailWidth = RailPrimaryButtonSize + RailGutter;

    /// <summary>底部控制带从下缘进入/退出的位移（与改动前的 80 一致）。</summary>
    public const double BottomEnterOffset = 80d;

    /// <summary>
    /// 只有小窗模式（CompactOverlay presenter）且实际可用区域确为纵向时才使用右侧轨道。
    /// 普通窗口即使被拉成纵向、自动比例关闭后的横向小窗、正方形窗口都不会切换。
    /// 尺寸取 XamlRoot.Size，即整个 XAML 岛（含 60 DIP 玩家标题栏）；这里按“窗口客户区形态”
    /// 比较 高 &gt; 宽 是刻意口径，与用户把窗口拉成长竖条的语义一致。不扣头部高度，
    /// 否则 270 宽、330 高这类窗口会被误判成横屏而退回底部带。
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
    /// 入场/退场位移：底部带从下缘进出，竖屏轨道从右缘进出。
    /// 两个方向必须分开，否则轨道会跟着底部带从下方出现（用户明确否定的旧实现）。
    /// </summary>
    public static (double X, double Y) EnterOffset(OverlayControlLayoutMode mode)
        => mode == OverlayControlLayoutMode.PortraitRail
            ? (RailWidth, 0d)
            : (0d, BottomEnterOffset);

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
