using Richasy.WinUIKernel.Share.Base;
using System.Windows.Input;

namespace BiliCopilot.UI.Controls;

public sealed partial class IncrementVerticalScrollControl : LayoutUserControlBase
{
    private bool _checkPending;
    private bool _isActive;
    private int _loadVersion;
    private readonly TimeProvider _timeProvider;

    public static readonly DependencyProperty ElementProperty =
        DependencyProperty.Register(nameof(Element), typeof(object), typeof(IncrementVerticalScrollControl), new PropertyMetadata(default));

    public static readonly DependencyProperty LoadMoreCommandProperty =
        DependencyProperty.Register(nameof(LoadMoreCommand), typeof(ICommand), typeof(IncrementVerticalScrollControl), new PropertyMetadata(default));

    public IncrementVerticalScrollControl()
        : this(TimeProvider.System)
    {
    }

    internal IncrementVerticalScrollControl(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        InitializeComponent();
        // The NuGet base unregisters its lifecycle handlers on the first unload.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public object Element
    {
        get => (object)GetValue(ElementProperty);
        set => SetValue(ElementProperty, value);
    }

    public ICommand LoadMoreCommand
    {
        get => (ICommand)GetValue(LoadMoreCommandProperty);
        set => SetValue(LoadMoreCommandProperty, value);
    }

    public async Task DelayCheckItemsAsync()
    {
        if (!_isActive)
        {
            return;
        }

        var version = _loadVersion;
        await Task.Delay(TimeSpan.FromMilliseconds(500), _timeProvider);
        if (version == _loadVersion)
        {
            CheckItemsCount();
        }
    }

    public void ResetScrollPosition()
    {
        if (View is null)
        {
            return;
        }

        View.ScrollTo(0, 0);
    }

    public double GetScrollPosition()
    {
        if (View is null)
        {
            return 0;
        }

        return View.VerticalOffset;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        View.ViewChanged += OnViewChanged;
        View.SizeChanged += OnViewSizeChanged;
        CheckItemsCount();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        _loadVersion++;
        // Keep the pending slot until its callback drains, even across a reload.
        View.ViewChanged -= OnViewChanged;
        View.SizeChanged -= OnViewSizeChanged;
    }

    private void OnViewChanged(ScrollView sender, object e)
    {
        CheckItemsCount();
    }

    private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 100)
        {
            CheckItemsCount();
        }
    }

    private void CheckItemsCount()
    {
        if (!_isActive || _checkPending)
        {
            return;
        }

        _checkPending = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            // Reentrant layout can request one follow-up; always read the current view.
            _checkPending = false;
            if (_isActive && View.ExtentHeight - View.ViewportHeight - View.VerticalOffset <= 800)
            {
                var command = LoadMoreCommand;
                if (command?.CanExecute(default) == true)
                {
                    command.Execute(default);
                }
            }
        }))
        {
            _checkPending = false;
        }
    }
}
