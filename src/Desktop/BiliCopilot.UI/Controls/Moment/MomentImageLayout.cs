// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>Original-aspect single images and square multi-image tiles.</summary>
public sealed partial class MomentImageLayout : NonVirtualizingLayout
{
    public static readonly DependencyProperty SingleImageRatioProperty = DependencyProperty.Register(
        nameof(SingleImageRatio), typeof(double), typeof(MomentImageLayout),
        new PropertyMetadata(1d, (sender, _) => ((MomentImageLayout)sender).InvalidateMeasure()));

    public double SingleImageRatio
    {
        get => (double)GetValue(SingleImageRatioProperty);
        set => SetValue(SingleImageRatioProperty, value);
    }

    protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
    {
        var count = context.Children.Count;
        if (count == 0) return default;
        var columns = count == 1 ? 1 : count is 2 or 4 ? 2 : 3;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 368;
        var cellWidth = Math.Max(0, (width - (columns - 1) * 4) / columns);
        if (count == 1)
        {
            var imageHeight = Math.Min(600, width / SingleImageRatio);
            context.Children[0].Measure(new(imageHeight * SingleImageRatio, imageHeight));
            return new(width, imageHeight);
        }
        var height = cellWidth;
        foreach (var child in context.Children) child.Measure(new(cellWidth, height));
        var rows = (count + columns - 1) / columns;
        return new(width, rows * height + (rows - 1) * 4);
    }

    protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
    {
        var count = context.Children.Count;
        if (count == 0) return finalSize;
        if (count == 1)
        {
            context.Children[0].Arrange(new(0, 0, Math.Min(finalSize.Width, finalSize.Height * SingleImageRatio), finalSize.Height));
            return finalSize;
        }
        var columns = count is 2 or 4 ? 2 : 3;
        var width = Math.Max(0, (finalSize.Width - (columns - 1) * 4) / columns);
        var rows = (count + columns - 1) / columns;
        var height = Math.Max(0, (finalSize.Height - (rows - 1) * 4) / rows);
        for (var i = 0; i < count; i++)
        {
            context.Children[i].Arrange(new(i % columns * (width + 4), i / columns * (height + 4), width, height));
        }
        return finalSize;
    }
}
