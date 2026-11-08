// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

internal enum OverlayControlMode
{
    Normal,
    CompactLandscape,
    CompactPortrait,
}

/// <summary>Presentation selection only. XAML owns layout, popup orientation and motion.</summary>
internal static class OverlayControlLayout
{
    public static OverlayControlMode SelectMode(bool compact, double width, double height)
        => !compact ? OverlayControlMode.Normal
        : double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > width
            ? OverlayControlMode.CompactPortrait : OverlayControlMode.CompactLandscape;

    public static string StateName(OverlayControlMode mode, double width, bool danmaku)
        => mode switch
        {
            OverlayControlMode.CompactPortrait => "CompactPortraitState",
            OverlayControlMode.CompactLandscape => "CompactLandscapeState",
            _ when width < (danmaku ? 900 : 760) => "NarrowState",
            _ when width < 1100 => "MediumState",
            _ when width < 1280 => "BelowWideState",
            _ => "WideState",
        };

    public static bool Contains(OverlayControlMode mode, double width, double height, double x, double y)
        => double.IsFinite(width) && double.IsFinite(height) && double.IsFinite(x) && double.IsFinite(y)
            && width > 0 && height > 0 && x >= 0 && y >= 0 && x <= width && y <= height
            && (y >= height - 160 || (mode == OverlayControlMode.CompactPortrait && x >= width - 56));
}
