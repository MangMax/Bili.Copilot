// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Components;

/// <summary>Fits compact labels from the leading edge, giving the recommendation reason priority.</summary>
public sealed partial class PopularVideoLabelsPanel : Panel
{
    private const double Spacing = 6;
    private readonly List<double> _slots = new();

    /// <inheritdoc/>
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        _slots.Clear();
        var remaining = availableSize.Width;
        var width = 0d;
        var height = 0d;
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) { _slots.Add(0); continue; }
            child.Measure(new(double.PositiveInfinity, availableSize.Height));
            var gap = width > 0 ? Spacing : 0;
            // A fractional digit advance can round down during layout and falsely trigger ellipsis.
            var natural = Math.Ceiling(child.DesiredSize.Width) + 1;
            var allocated = Math.Min(natural, Math.Max(0, remaining - gap));
            _slots.Add(allocated);
            child.Measure(new(allocated, availableSize.Height));
            if (allocated <= 0) continue;
            width += gap + allocated;
            remaining -= gap + allocated;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new(width, height);
    }

    /// <inheritdoc/>
    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
    {
        var left = 0d;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (child.Visibility == Visibility.Collapsed) continue;
            var gap = left > 0 ? Spacing : 0;
            // Keep the measured slot instead of shrinking it back to fractional DesiredSize.
            var allocated = i < _slots.Count ? _slots[i] : 0;
            var width = Math.Min(allocated, Math.Max(0, finalSize.Width - left - gap));
            child.Arrange(new(left + gap, 0, width, finalSize.Height));
            if (width > 0) left += gap + width;
        }
        return finalSize;
    }
}
