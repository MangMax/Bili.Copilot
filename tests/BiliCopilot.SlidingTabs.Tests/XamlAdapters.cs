// Only platform surfaces are adapted. The actual production control is linked unchanged.
global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
global using Windows.Foundation;

namespace Microsoft.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
    public class DependencyObject
    {
        public List<Microsoft.UI.Xaml.Media.Animation.DoubleAnimation> Animations { get; } = new();
    }
    public class RoutedEventArgs : EventArgs { }
    public class FrameworkElement : DependencyObject
    {
        public event Action<object, RoutedEventArgs>? Loaded;
        public event Action<object, RoutedEventArgs>? Unloaded;
        public event EventHandler<object>? LayoutUpdated;
        public double ActualWidth { get; set; }
        public double ActualHeight { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public Visibility Visibility { get; set; } = Visibility.Collapsed;
        public Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; } = new();
        public XamlRoot XamlRoot { get; } = new();
        public Microsoft.UI.Xaml.Media.GeneralTransform TransformToVisual(FrameworkElement other) => new(Left, Top);
        public int BringIntoViewCount { get; private set; }
        public void StartBringIntoView() => BringIntoViewCount++;
        public void RaiseLayoutUpdated() => LayoutUpdated?.Invoke(this, new());
        public void RaiseLoaded() => Loaded?.Invoke(this, new());
        public void RaiseUnloaded() => Unloaded?.Invoke(this, new());
    }
    public class XamlRootChangedEventArgs : EventArgs { }
    public class XamlRoot
    {
        public event Action<XamlRoot, XamlRootChangedEventArgs>? Changed;
        public void RaiseChanged() => Changed?.Invoke(this, new());
    }
}

namespace Microsoft.UI.Xaml.Controls
{
    public class UserControl : FrameworkElement { }
    public class Border : FrameworkElement { }
    public class Grid : FrameworkElement { }
    public class SelectorBarSelectionChangedEventArgs : EventArgs { }
    public class SelectorBarItem : FrameworkElement
    {
        public string? Text { get; set; }
        public object? Tag { get; set; }
    }
    public class SelectorBar : FrameworkElement
    {
        private SelectorBarItem? _selectedItem;
        public List<SelectorBarItem> Items { get; } = new();
        public SelectorBarItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (ReferenceEquals(_selectedItem, value)) return;
                _selectedItem = value;
                SelectionChanged?.Invoke(this, new());
            }
        }
        public event Action<SelectorBar, SelectorBarSelectionChangedEventArgs>? SelectionChanged;
    }
}

namespace Microsoft.UI.Xaml.Automation
{
    public static class AutomationProperties
    {
        public static void SetAutomationId(DependencyObject target, string id) { }
        public static void SetName(DependencyObject target, string name) { }
    }
}

namespace BiliCopilot.UI.Controls.Components
{
    public sealed partial class SlidingTabBar
    {
        private SelectorBar Selector = null!;
        private Border Indicator = new();
        private Grid Track = new();
        private Microsoft.UI.Xaml.Media.TranslateTransform IndicatorTranslation = new();
        public Border TestIndicator => Indicator;
        public Windows.UI.ViewManagement.UISettings TestSettings => _settings;
        public Microsoft.UI.Xaml.Media.TranslateTransform TestTranslation => IndicatorTranslation;
        public SelectorBar TestSelector => Selector;
        private void InitializeComponent() => Selector = new();
    }
}

namespace Windows.Foundation
{
    public readonly record struct Point(double X, double Y);
    public readonly record struct Rect(double X, double Y, double Width, double Height);
}

namespace Microsoft.UI.Xaml.Media
{
    public class TranslateTransform : DependencyObject { public double X { get; set; } public double Y { get; set; } }
    public class GeneralTransform(double x, double y)
    {
        public Windows.Foundation.Point TransformPoint(Windows.Foundation.Point point) => new(x + point.X, y + point.Y);
    }
}

namespace Microsoft.UI.Xaml.Media.Animation
{
    public enum EasingMode { EaseOut }
    public class CubicEase { public EasingMode EasingMode { get; set; } }
    public class DoubleAnimation
    {
        public double From { get; set; }
        public double To { get; set; }
        public TimeSpan Duration { get; set; }
        public CubicEase? EasingFunction { get; set; }
        public bool EnableDependentAnimation { get; set; }
        public DependencyObject Target { get; set; } = null!;
        public string Path { get; set; } = "";
    }
    public class Storyboard
    {
        public List<DoubleAnimation> Children { get; } = new();
        public void Begin() { foreach (var child in Children) child.Target.Animations.Add(child); }
        public void Stop() { foreach (var child in Children) child.Target.Animations.Remove(child); }
        public static void SetTarget(DoubleAnimation animation, DependencyObject target) => animation.Target = target;
        public static void SetTargetProperty(DoubleAnimation animation, string path) => animation.Path = path;
    }
}

namespace Windows.UI.ViewManagement
{
    public class UISettingsAnimationsEnabledChangedEventArgs : EventArgs { }
    public class UISettings
    {
        public bool AnimationsEnabled { get; set; } = true;
        public event Action<UISettings, UISettingsAnimationsEnabledChangedEventArgs>? AnimationsEnabledChanged;
        public void RaiseAnimationsEnabledChanged() => AnimationsEnabledChanged?.Invoke(this, new());
    }
}

namespace Microsoft.UI.Dispatching
{
    public class DispatcherQueue
    {
        private readonly Queue<Action> _pending = new();
        public bool TryEnqueue(Action callback) { _pending.Enqueue(callback); return true; }
        public void Drain() { while (_pending.TryDequeue(out var callback)) callback(); }
    }
}
