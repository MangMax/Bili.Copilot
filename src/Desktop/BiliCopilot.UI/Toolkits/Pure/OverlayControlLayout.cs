// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

internal enum OverlayControlMode
{
    Fill,
    CompactLandscape,
    CompactPortrait,
    Default,
    FullScreen,
}

internal enum OverlayWindowMode
{
    Other,
    Overlapped,
    Compact,
    FullScreen,
}

internal readonly record struct OverlayPresentation(OverlayControlMode Mode, bool CanToggleLayout)
{
    public bool UsesDefaultLayout => Mode == OverlayControlMode.Default;
}

/// <summary>Presentation selection only. XAML owns layout, popup orientation and motion.</summary>
internal static class OverlayControlLayout
{
    private const double MinimumVideoWidth = 620;
    private const double SidebarAspectRatio = 4d / 3;

    public static OverlayPresentation Resolve(OverlayWindowMode window, bool supportsDetails, bool prefersDetails, double width, double height)
    {
        var canToggleLayout = window == OverlayWindowMode.Overlapped && supportsDetails;
        var mode = window switch
        {
            OverlayWindowMode.Compact when double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > width
                => OverlayControlMode.CompactPortrait,
            OverlayWindowMode.Compact => OverlayControlMode.CompactLandscape,
            OverlayWindowMode.FullScreen => OverlayControlMode.FullScreen,
            _ when canToggleLayout && prefersDetails => OverlayControlMode.Default,
            _ => OverlayControlMode.Fill,
        };
        return new(mode, canToggleLayout);
    }

    public static double GetSidebarWidth(OverlayPresentation presentation, double width, double height, double minimumWidth, double preferredWidth)
    {
        var availableWidth = width - MinimumVideoWidth;
        return presentation.UsesDefaultLayout && width > height * SidebarAspectRatio && availableWidth >= minimumWidth
            ? Math.Clamp(preferredWidth, minimumWidth, availableWidth)
            : 0;
    }

    public static bool ShowsDetailsTime(OverlayControlMode mode, double width)
        => mode == OverlayControlMode.Default && width >= 740;

    public static (string Layout, string Controls) SelectVisualStates(OverlayControlMode mode, double width, bool danmaku)
        => mode switch
        {
            OverlayControlMode.CompactPortrait => ("CompactPortraitLayout", "CompactPortraitState"),
            OverlayControlMode.CompactLandscape => ("CompactControlsLayout", "NarrowState"),
            OverlayControlMode.Default => ("DefaultLayout", width switch
            {
                < 650 => "DefaultMinimumWidthState",
                < 1100 => "DefaultNarrowState",
                _ => "DefaultWideState",
            }),
            OverlayControlMode.Fill or OverlayControlMode.FullScreen => width switch
            {
                _ when width < (danmaku ? 900 : 760) => ("CompactControlsLayout", "NarrowState"),
                < 1100 => ("OverlayWithoutCardLayout", "MediumState"),
                < 1280 => ("OverlayLayout", "BelowWideState"),
                _ => ("OverlayLayout", "WideState"),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

    public static bool Contains(OverlayControlMode mode, double width, double height, double x, double y)
        => double.IsFinite(width) && double.IsFinite(height) && double.IsFinite(x) && double.IsFinite(y)
            && width > 0 && height > 0 && x >= 0 && y >= 0 && x <= width && y <= height
            && (y >= height - 160 || (mode == OverlayControlMode.CompactPortrait && x >= width - 56));
}
