// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// Keeps a remembered compact-overlay position unless the window would be entirely
/// outside its work area, for example after a monitor was removed.
/// </summary>
/// <remarks>
/// A compact window may deliberately overhang an edge when it is tucked into a screen
/// corner, so any visible part of it is kept where the user left it. Only a window
/// that the user could not see (and therefore could not drag back) is recovered.
/// </remarks>
internal static class CompactOverlayPlacement
{
    /// <summary>
    /// Returns the origin on one axis, recovered into the work area only when the window
    /// would be entirely outside it on that axis.
    /// </summary>
    /// <param name="position">Window origin in physical pixels.</param>
    /// <param name="size">Window size in physical pixels.</param>
    /// <param name="areaStart">Work area origin in physical pixels.</param>
    /// <param name="areaSize">Work area size in physical pixels.</param>
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
