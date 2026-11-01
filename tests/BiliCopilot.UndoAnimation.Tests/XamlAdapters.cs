// Only XAML animation surfaces are adapted; the product animator is linked unchanged.
global using Microsoft.UI.Xaml;
namespace Microsoft.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
    public class DependencyObject { public DependencyObject? Parent { get; set; } }
    public class UIElement : DependencyObject { public Visibility Visibility { get; set; } }
    public class FrameworkElement : UIElement
    {
        public bool IsLoaded { get; set; } = true;
        public double Opacity { get; set; } = 1;
        public object? RenderTransform { get; set; }
    }
}
namespace Microsoft.UI.Xaml.Media
{
    public class CompositeTransform : DependencyObject { public double ScaleX { get; set; } = 1; public double ScaleY { get; set; } = 1; }
    public static class VisualTreeHelper { public static DependencyObject? GetParent(DependencyObject value) => value.Parent; }
}
namespace Microsoft.UI.Xaml.Media.Animation
{
    public enum FillBehavior { Stop, HoldEnd }
    public enum EasingMode { EaseOut }
    public class CubicEase { public EasingMode EasingMode { get; set; } }
    public class DoubleAnimation
    {
        public double From { get; set; } public double To { get; set; }
        public TimeSpan Duration { get; set; } public CubicEase? EasingFunction { get; set; }
        public FillBehavior FillBehavior { get; set; } public bool EnableDependentAnimation { get; set; }
    }
    public class Storyboard
    {
        public static List<Storyboard> Begun { get; } = new();
        public List<DoubleAnimation> Children { get; } = new();
        public event EventHandler? Completed;
        public int StopCount { get; private set; }
        public void Begin() => Begun.Add(this);
        public void Stop() => StopCount++;
        public void Complete() => Completed?.Invoke(this, EventArgs.Empty);
        public static void SetTarget(DoubleAnimation value, DependencyObject target) { }
        public static void SetTargetProperty(DoubleAnimation value, string path) { }
    }
}
