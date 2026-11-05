// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Components;

/// <summary>Measures a popular card's 16:10 cover independently from its metadata.</summary>
public sealed partial class PopularVideoCardPanel : Panel
{
    /// <inheritdoc/>
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        if (Children.Count != 2) return default;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 232;
        var scale = XamlRoot?.RasterizationScale ?? 1;
        var coverHeight = Math.Ceiling(width / 1.6 * scale) / scale;
        Children[0].Measure(new(width, coverHeight));
        Children[1].Measure(new(width, double.PositiveInfinity));
        return new(width, coverHeight + Children[1].DesiredSize.Height);
    }

    /// <inheritdoc/>
    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
    {
        if (Children.Count != 2) return finalSize;
        // The grid rounds column widths and row heights independently. Recomputing
        // the cover from the final width can exceed the height we requested in
        // MeasureOverride. Preserve the complete metadata area and give the cover
        // the remaining arranged height instead.
        var coverHeight = Math.Max(0, finalSize.Height - Children[1].DesiredSize.Height);
        Children[0].Arrange(new(0, 0, finalSize.Width, coverHeight));
        Children[1].Arrange(new(0, coverHeight, finalSize.Width, Children[1].DesiredSize.Height));
        return finalSize;
    }
}
