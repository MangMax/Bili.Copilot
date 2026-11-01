// Minimal XAML tree surface for the real RecommendationScrollAnchor source.
// Cards are laid out in one column; the test owns the layout order and the
// realized window, so a card outside the viewport cache is absent from the tree.
namespace Microsoft.UI.Xaml
{
    public class DependencyObject
    {
        public List<DependencyObject> Children { get; } = new();
        public double LayoutTop { get; set; }
    }

    public class FrameworkElement : DependencyObject
    {
        public bool IsLoaded { get; set; } = true;
        public double ActualHeight { get; set; }

        // The production code calls UpdateLayout before reading card positions.
        // Tests use this hook to re-realize the window for the current offset.
        public Action? LayoutPass { get; set; }

        public void UpdateLayout() => LayoutPass?.Invoke();

        public TestGeneralTransform TransformToVisual(object reference)
            => new(this, (BiliCopilot.UI.Controls.ScrollView)reference);
    }

    public sealed class TestGeneralTransform(DependencyObject element, BiliCopilot.UI.Controls.ScrollView scroll)
    {
        public Point TransformPoint(Point point) => new(point.X, element.LayoutTop - scroll.VerticalOffset);
    }

    public readonly struct Point(double x, double y)
    {
        public double X { get; } = x;
        public double Y { get; } = y;
    }

    public enum ScrollingAnimationMode
    {
        Disabled,
    }

    public sealed class ScrollingScrollOptions(ScrollingAnimationMode animationMode)
    {
        public ScrollingAnimationMode AnimationMode { get; } = animationMode;
    }
}

namespace Microsoft.UI.Xaml.Media
{
    public static class VisualTreeHelper
    {
        public static int GetChildrenCount(DependencyObject element) => element.Children.Count;
        public static DependencyObject GetChild(DependencyObject element, int index) => element.Children[index];
    }
}

namespace BiliCopilot.UI.Controls.Components
{
    public sealed class VideoCardControl : Microsoft.UI.Xaml.FrameworkElement
    {
        public object? ViewModel { get; set; }
    }
}
