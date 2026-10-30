// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>Pixel dimensions sufficient for a UniformToFill cover at the current DPI.</summary>
internal static class CoverDecodeSize
{
    public static (uint Width, uint Height) Calculate(uint sourceWidth, uint sourceHeight,
        double displayWidth, double displayHeight, double rasterizationScale)
    {
        if (sourceWidth == 0 || sourceHeight == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        }

        if (!double.IsFinite(displayWidth) || displayWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayWidth));
        }

        if (!double.IsFinite(displayHeight) || displayHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayHeight));
        }

        if (!double.IsFinite(rasterizationScale) || rasterizationScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rasterizationScale));
        }

        var ratio = Math.Min(1, Math.Max(displayWidth * rasterizationScale / sourceWidth,
            displayHeight * rasterizationScale / sourceHeight));
        return (Math.Max(1, (uint)Math.Ceiling(sourceWidth * ratio)),
            Math.Max(1, (uint)Math.Ceiling(sourceHeight * ratio)));
    }
}
