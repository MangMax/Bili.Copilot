using BiliCopilot.UI.Controls.Components;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;

namespace BiliCopilot.UI.Controls;

/// <summary>Preserves a visible card across a recommendation diff and grid reflow.</summary>
internal sealed class RecommendationScrollAnchor(FrameworkElement root, IncrementVerticalScrollControl scrollControl)
{
    private readonly ScrollView scroll = (ScrollView)scrollControl.FindName("View");
    private object? _item;
    private double _top;
    private int _version;
    private bool _undo;

    public void Capture(bool undo)
    {
        ++_version;
        _undo = undo;
        // Undo restores the pre-refresh viewport, not a different card that happened
        // to move into the first grid column after the prepend.
        if (undo) return;
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
        var version = _version;
        var item = _item;
        var top = _top;
        if (_undo) _item = null;
        scroll.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (version != _version || item is null || !root.IsLoaded) return;
            root.UpdateLayout();
            var card = Cards(root).FirstOrDefault(c => ReferenceEquals(c.ViewModel, item));
            if (card is null) return;
            var current = card.TransformToVisual(scroll).TransformPoint(new(0, 0)).Y;
            scroll.ScrollTo(scroll.HorizontalOffset, Math.Max(0, scroll.VerticalOffset + current - top),
                new ScrollingScrollOptions(ScrollingAnimationMode.Disabled));
        });
    }

    public void Cancel()
    {
        ++_version;
        _item = null;
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
