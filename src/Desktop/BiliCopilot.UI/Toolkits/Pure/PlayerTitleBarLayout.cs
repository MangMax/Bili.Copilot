// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>All title-bar modes reserve native caption space before placing app input.</summary>
internal readonly record struct PlayerTitleBarLayout(double Left, double Right, double Height, bool ShowActions)
{
    public static PlayerTitleBarLayout Create(double width, double height, double scale,
        int leftInset, int rightInset, bool compact)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(scale)
            || width <= 0 || height <= 0 || scale <= 0)
        {
            return default;
        }

        var left = Math.Clamp(leftInset / scale, 0, width);
        var right = Math.Clamp(width - Math.Max(0, rightInset) / scale, left, width);
        return new(left, right, height, !compact && width >= 700);
    }

    public (double X, double Y, double Width, double Height) Clip(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width)
            || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return default;
        }

        var left = Math.Max(Left, x);
        var top = Math.Max(0, y);
        var right = Math.Min(Right, x + width);
        var bottom = Math.Min(Height, y + height);
        return right > left && bottom > top ? (left, top, right - left, bottom - top) : default;
    }
}
