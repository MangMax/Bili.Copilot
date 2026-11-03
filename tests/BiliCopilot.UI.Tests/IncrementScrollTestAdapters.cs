global using Microsoft.UI.Xaml;

// Only this control's Windows presentation surface; no pagination logic is copied.
namespace Richasy.WinUIKernel.Share.Base
{
    public abstract class LayoutUserControlBase
    {
        protected LayoutUserControlBase()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public event Microsoft.UI.Xaml.RoutedEventHandler? Loaded;
        public event Microsoft.UI.Xaml.RoutedEventHandler? Unloaded;
        private readonly Dictionary<object, object> _values = new();
        public BiliCopilot.UI.Controls.ScrollTestDispatcher DispatcherQueue { get; } = new();
        public object GetValue(object property) => _values.GetValueOrDefault(property)!;
        public void SetValue(object property, object value) => _values[property] = value;
        public void Load() => Loaded?.Invoke(this, new());
        public void Unload() => Unloaded?.Invoke(this, new());
        private void OnLoaded(object? sender, Microsoft.UI.Xaml.RoutedEventArgs args) => OnControlLoaded();
        private void OnUnloaded(object? sender, Microsoft.UI.Xaml.RoutedEventArgs args)
        {
            // NuGet 0.0.12-preview3 unregisters its own handlers on the first unload.
            // Model only that event boundary; real XAML/bindings remain a UI-fixture concern.
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            OnControlUnloaded();
        }
        protected virtual void OnControlLoaded() { }
        protected virtual void OnControlUnloaded() { }
    }
}

namespace Microsoft.UI.Xaml
{
    public delegate void RoutedEventHandler(object sender, RoutedEventArgs args);
    public sealed class RoutedEventArgs : EventArgs { }
}

namespace BiliCopilot.UI.Controls
{
    public sealed class DependencyProperty
    {
        public static DependencyProperty Register(string name, Type type, Type owner, PropertyMetadata metadata) => new();
    }
    public sealed class PropertyMetadata(object? value)
    {
        public object? DefaultValue { get; } = value;
    }
    public sealed class SizeChangedEventArgs(double width, double height = 600) : EventArgs
    {
        public (double Width, double Height) NewSize => (width, height);
    }
    public sealed class ScrollView
    {
        public double ExtentHeight { get; set; }
        public double ViewportHeight { get; set; } = 600;
        public double VerticalOffset { get; set; }
        public double HorizontalOffset { get; set; }
        public double ActualWidth { get; set; } = 1000;
        public double ActualHeight => ViewportHeight;
        public ScrollTestDispatcher DispatcherQueue { get; } = new();
        public event Action<ScrollView, object>? ViewChanged;
        public event Action<object, SizeChangedEventArgs>? SizeChanged;
        public int ViewChangedSubscriptions => ViewChanged?.GetInvocationList().Length ?? 0;
        public int SizeChangedSubscriptions => SizeChanged?.GetInvocationList().Length ?? 0;
        public void ChangeView() => ViewChanged?.Invoke(this, EventArgs.Empty);
        public void Resize(double width = 1000, double height = 600) => SizeChanged?.Invoke(this, new(width, height));
        public void ScrollTo(double x, double y) => VerticalOffset = y;
        public void ScrollTo(double x, double y, ScrollingScrollOptions options) => VerticalOffset = y;
    }
    public sealed class ScrollTestDispatcher
    {
        private readonly Queue<Action> _callbacks = new();
        public bool Accept { get; set; } = true;
        public int PendingCount => _callbacks.Count;
        public bool TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority priority, Action callback)
        {
            if (!Accept) return false;
            _callbacks.Enqueue(callback);
            return true;
        }
        public void RunNext() => _callbacks.Dequeue()();
        public void Drain()
        {
            for (var i = 0; _callbacks.Count > 0; i++)
            {
                if (i == 100) throw new InvalidOperationException("Scroll checks did not become idle.");
                RunNext();
            }
        }
    }
    public sealed partial class IncrementVerticalScrollControl
    {
        public ScrollView View { get; } = new();
        public object FindName(string name) => View;
        private void InitializeComponent() { }
    }
}
