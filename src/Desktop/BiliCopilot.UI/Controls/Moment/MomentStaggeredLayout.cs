// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 瀑布流布局：每次把卡片放进当前最矮的一列，卡片高度不同也能从顶部对齐排布.
/// </summary>
public sealed partial class MomentStaggeredLayout : NonVirtualizingLayout
{
    private readonly List<(double Top, int Column)> _placements = [];
    private double _columnWidth;

    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(MomentStaggeredLayout),
        new PropertyMetadata(320d, (sender, _) => ((MomentStaggeredLayout)sender).InvalidateMeasure()));

    public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
        nameof(ColumnSpacing), typeof(double), typeof(MomentStaggeredLayout),
        new PropertyMetadata(16d, (sender, _) => ((MomentStaggeredLayout)sender).InvalidateMeasure()));

    public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
        nameof(RowSpacing), typeof(double), typeof(MomentStaggeredLayout),
        new PropertyMetadata(16d, (sender, _) => ((MomentStaggeredLayout)sender).InvalidateMeasure()));

    /// <summary>
    /// 单列的最小宽度，用它决定列数.
    /// </summary>
    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    /// <summary>
    /// 列间距.
    /// </summary>
    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// 行间距.
    /// </summary>
    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
    {
        var children = context.Children;
        var width = availableSize.Width;
        if (!double.IsFinite(width) || width <= 0)
        {
            width = MinItemWidth;
        }

        var columns = GetColumnCount(width);
        _columnWidth = Math.Max(0, (width - ((columns - 1) * ColumnSpacing)) / columns);
        var heights = new double[columns];
        _placements.Clear();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            child.Measure(new Size(_columnWidth, double.PositiveInfinity));
            var column = 0;
            for (var candidate = 1; candidate < columns; candidate++)
            {
                if (heights[candidate] < heights[column])
                {
                    column = candidate;
                }
            }

            _placements.Add((heights[column], column));
            heights[column] += child.DesiredSize.Height + RowSpacing;
        }

        var height = 0d;
        foreach (var columnHeight in heights)
        {
            height = Math.Max(height, columnHeight);
        }

        return new Size(width, Math.Max(0, height - RowSpacing));
    }

    protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
    {
        var children = context.Children;
        if (_placements.Count != children.Count)
        {
            // 没有与本次子项对应的测量结果时退回单列堆叠，至少保证内容可见.
            var offset = 0d;
            for (var i = 0; i < children.Count; i++)
            {
                children[i].Arrange(new Rect(0, offset, finalSize.Width, children[i].DesiredSize.Height));
                offset += children[i].DesiredSize.Height + RowSpacing;
            }

            return finalSize;
        }

        for (var i = 0; i < children.Count; i++)
        {
            var (top, column) = _placements[i];
            children[i].Arrange(new Rect(
                column * (_columnWidth + ColumnSpacing),
                top,
                _columnWidth,
                children[i].DesiredSize.Height));
        }

        return finalSize;
    }

    private int GetColumnCount(double width)
    {
        // 列数只由可用宽度和最小列宽决定：宽屏自动加列，不设上限.
        var itemWidth = MinItemWidth > 0 ? MinItemWidth : 320;
        var columns = (int)Math.Floor((width + ColumnSpacing) / (itemWidth + ColumnSpacing));
        return Math.Max(1, columns);
    }
}
