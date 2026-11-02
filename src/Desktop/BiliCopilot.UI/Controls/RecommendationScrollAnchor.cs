using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;

namespace BiliCopilot.UI.Controls;

/// <summary>Keeps a visible card across a recommendation diff, or sets the viewport after an incremental refresh.</summary>
internal sealed class RecommendationScrollAnchor(FrameworkElement root, IncrementVerticalScrollControl scrollControl)
{
    private readonly ScrollView scroll = (ScrollView)scrollControl.FindName("View");
    private object? _item;
    private double _top;
    private double _offset;
    private bool _hasOffset;
    private int _version;
    private bool _capturePending;
    private RecommendationListChange _change;

    public void Capture(RecommendationListChange change)
    {
        ++_version;
        _capturePending = true;
        _change = change;
        // Undo restores the pre-refresh viewport, not the current one. Keep the
        // offset recorded when the refresh started; after the reset the old card
        // can be recycled with a stale transform, so item-relative math is not reliable.
        if (change == RecommendationListChange.UndoRefresh) return;
        _offset = scroll.VerticalOffset;
        _hasOffset = true;
        _item = null;
        foreach (var card in Cards(root))
        {
            var top = card.TransformToVisual(scroll).TransformPoint(new(0, 0)).Y;
            if (top + card.ActualHeight > 0 && top < scroll.ActualHeight)
            {
                _item = card.ViewModel;
                _top = top;
                break;
            }
        }
    }

    public void Restore(object? sender, EventArgs args)
    {
        if (!_capturePending) return;
        _capturePending = false;
        var version = _version;
        var change = _change;
        var item = _item;
        var top = _top;
        var offset = _offset;
        var hasOffset = _hasOffset;
        if (change == RecommendationListChange.UndoRefresh) _item = null;
        scroll.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (version != _version || !root.IsLoaded) return;
            root.UpdateLayout();
            if (version != _version || !root.IsLoaded) return;
            if (change != RecommendationListChange.PreserveVisibleCard)
            {
                // Both reset and undo have an absolute target. Undo restores the offset
                // captured before the refresh, so it does not depend on the old card
                // still being realized after the list was reset to the top.
                if (change == RecommendationListChange.UndoRefresh && !hasOffset) return;
                scroll.ScrollTo(scroll.HorizontalOffset,
                    change == RecommendationListChange.ResetToTop ? 0 : offset,
                    new ScrollingScrollOptions(ScrollingAnimationMode.Disabled));
                return;
            }

            if (item is null) return;
            var card = Cards(root).FirstOrDefault(c => ReferenceEquals(c.ViewModel, item));
            if (card is null) return;
            var current = card.TransformToVisual(scroll).TransformPoint(new(0, 0)).Y;
            if (version != _version || !root.IsLoaded) return;
            scroll.ScrollTo(scroll.HorizontalOffset, Math.Max(0, scroll.VerticalOffset + current - top),
                new ScrollingScrollOptions(ScrollingAnimationMode.Disabled));
        });
    }

    public void Cancel()
    {
        ++_version;
        _capturePending = false;
        _item = null;
        _hasOffset = false;
    }

    private static IEnumerable<VideoCardControl> Cards(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is VideoCardControl card) yield return card;
            else foreach (var descendant in Cards(child)) yield return descendant;
        }
    }
}
