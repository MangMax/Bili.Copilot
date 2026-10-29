// Only this control's Windows presentation surface; no pagination logic is copied.
namespace Richasy.WinUIKernel.Share.Base
{
    public abstract class LayoutUserControlBase
    {
        private readonly Dictionary<object, object> _values = new();
        public BiliCopilot.UI.Controls.ScrollTestDispatcher DispatcherQueue { get; } = new();
        public object GetValue(object property) => _values.GetValueOrDefault(property)!;
        public void SetValue(object property, object value) => _values[property] = value;
        public void Load() => OnControlLoaded();
        public void Unload() => OnControlUnloaded();
        protected abstract void OnControlLoaded();
        protected abstract void OnControlUnloaded();
    }
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
    public sealed class SizeChangedEventArgs(double width) : EventArgs
    {
        public (double Width, double Height) NewSize => (width, 600);
    }
    public sealed class ScrollView
    {
        public double ExtentHeight { get; set; }
        public double ViewportHeight { get; set; } = 600;
        public double VerticalOffset { get; set; }
        public event Action<ScrollView, object>? ViewChanged;
        public event Action<object, SizeChangedEventArgs>? SizeChanged;
        public void ChangeView() => ViewChanged?.Invoke(this, EventArgs.Empty);
        public void Resize(double width = 1000) => SizeChanged?.Invoke(this, new(width));
        public void ScrollTo(double x, double y) => VerticalOffset = y;
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
        private void InitializeComponent() { }
    }
}
