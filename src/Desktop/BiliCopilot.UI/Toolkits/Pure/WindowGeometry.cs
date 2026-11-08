// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>A physical-pixel window origin.</summary>
internal readonly record struct WindowPoint(int X, int Y)
{
    public string ToSetting() => FormattableString.Invariant($"{X},{Y}");

    public static WindowPoint? ParseSetting(string value)
    {
        var coordinates = value.Split(',');
        return coordinates.Length == 2
            && int.TryParse(coordinates[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var x)
            && int.TryParse(coordinates[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var y)
            ? new WindowPoint(x, y)
            : null;
    }
}

/// <summary>A physical-pixel window rectangle.</summary>
internal readonly record struct WindowRect(int X, int Y, int Width, int Height)
{
    public override string ToString()
        => FormattableString.Invariant($"{X},{Y},{Width},{Height}");
}

/// <summary>The three logical presentation modes of a player window.</summary>
internal enum WindowPresentationMode
{
    Ordinary,
    CompactOverlay,
    FullScreen,
}
