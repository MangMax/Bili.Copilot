// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>Aspect-corrected video output dimensions, independent of window geometry.</summary>
internal readonly record struct MediaDisplayDimensions(double Width, double Height)
{
    public static MediaDisplayDimensions FromOutput(double width, double height, double rotation)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(rotation) || width <= 0 || height <= 0)
        {
            return default;
        }

        var radians = (rotation % 360) * Math.PI / 180;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));
        return new((width * cos) + (height * sin), (width * sin) + (height * cos));
    }
}
