// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// Pure compact-overlay geometry: two media forms, a work-area fit and the
/// "only recover a fully invisible window" position policy.
/// </summary>
internal static class CompactOverlayLayout
{
    /// <summary>The only landscape compact form.</summary>
    public const double LandscapeRatio = 16d / 9d;

    /// <summary>The only portrait compact form.</summary>
    public const double PortraitRatio = 9d / 16d;

    /// <summary>
    /// Selects the compact form for a source ratio: square or wider content uses the
    /// landscape form, anything taller uses the portrait form.
    /// </summary>
    public static double SelectForm(double sourceRatio)
        => sourceRatio >= 1 ? LandscapeRatio : PortraitRatio;

    /// <summary>
    /// Fits the compact client area to one of the two forms, keeping the current longer
    /// client edge and the requested top-left anchor, then scales down to the work area.
    /// </summary>
    /// <param name="anchor">Desired frame origin in physical pixels.</param>
    /// <param name="baseline">Current frame rectangle in physical pixels.</param>
    /// <param name="frameWidth">Non-client width in physical pixels.</param>
    /// <param name="frameHeight">Non-client height in physical pixels.</param>
    /// <param name="area">Work area in physical pixels.</param>
    /// <param name="form">Landscape or portrait form.</param>
    public static WindowRect Fit(WindowPoint anchor, WindowRect baseline, int frameWidth, int frameHeight,
        WindowRect area, double form)
    {
        var clientWidth = Math.Max(1, baseline.Width - frameWidth);
        var clientHeight = Math.Max(1, baseline.Height - frameHeight);
        var edge = Math.Max(clientWidth, clientHeight);
        var width = form >= 1 ? edge : edge * form;
        var height = form >= 1 ? edge / form : edge;
        var availableWidth = Math.Max(1, area.Width - frameWidth);
        var availableHeight = Math.Max(1, area.Height - frameHeight);
        var scale = Math.Min(1, Math.Min(availableWidth / width, availableHeight / height));
        var targetWidth = Math.Clamp((int)Math.Round(width * scale), 1, availableWidth);
        var targetHeight = Math.Clamp((int)Math.Round(height * scale), 1, availableHeight);
        var frameW = targetWidth + frameWidth;
        var frameH = targetHeight + frameHeight;
        return new WindowRect(
            RecoverAxis(anchor.X, frameW, area.X, area.Width),
            RecoverAxis(anchor.Y, frameH, area.Y, area.Height),
            frameW,
            frameH);
    }

    /// <summary>
    /// Moves a frame only when it is entirely outside the work area; any visible part
    /// (including a deliberate overhang) keeps its position.
    /// </summary>
    public static WindowRect Recover(WindowRect bounds, WindowRect area)
        => new(RecoverAxis(bounds.X, bounds.Width, area.X, area.Width),
            RecoverAxis(bounds.Y, bounds.Height, area.Y, area.Height),
            bounds.Width,
            bounds.Height);

    /// <summary>
    /// Returns the origin on one axis, recovered into the work area only when the frame
    /// would be entirely outside it on that axis.
    /// </summary>
    public static int RecoverAxis(int position, int size, int areaStart, int areaSize)
    {
        var start = (long)areaStart;
        var end = (long)areaStart + areaSize;
        var value = (long)position;
        if (value + size > start && value < end)
        {
            return (int)value;
        }

        // A window larger than the work area keeps its top-left controls visible.
        var maximum = Math.Max(start, end - size);
        return (int)Math.Clamp(value, start, maximum);
    }
}
