// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// A segmented selector with a shared moving selection background.
/// Items use their Tag as the caller's selection identity; no business commands live here.
/// </summary>
public sealed partial class SlidingTabBar : UserControl
{
    private bool _synchronizing;
    private bool _attached;
    private readonly UISettings _settings = new();
    private Rect? _target;
    private Storyboard? _transition;
    private bool _animateNextLayout;
    private XamlRoot? _root;
    private int _mountVersion;

    /// <summary>Initializes a new instance of the <see cref="SlidingTabBar"/> class.</summary>
    public SlidingTabBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Raised only for a user selection, never when synchronizing caller state.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets the selected item's identity.</summary>
    public object? SelectedItem => Selector.SelectedItem?.Tag;

    /// <summary>Replaces the available items and applies the caller's current selection.</summary>
    public void SetItems(IEnumerable<SelectorBarItem> items, object? selectedItem)
    {
        HideIndicator();
        _synchronizing = true;
        try
        {
            Selector.Items.Clear();
            foreach (var item in items)
            {
                Selector.Items.Add(item);
            }

            Select(selectedItem);
        }
        finally
        {
            _synchronizing = false;
        }
    }

    /// <summary>Synchronizes an external selection without echoing a user event.</summary>
    public void Select(object? selectedItem)
    {
        var previous = SelectedItem;
        var wasSynchronizing = _synchronizing;
        _synchronizing = true;
        try
        {
            Selector.SelectedItem = selectedItem is null
                ? null
                : Selector.Items.FirstOrDefault(item => ReferenceEquals(item.Tag, selectedItem));
        }
        finally
        {
            _synchronizing = wasSynchronizing;
        }

        if (!ReferenceEquals(previous, SelectedItem))
        {
            SelectionUpdated();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _mountVersion++;
        Selector.SelectionChanged += OnSelectionChanged;
        LayoutUpdated += OnLayoutUpdated;
        _settings.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
        _root = XamlRoot;
        if (_root is not null)
        {
            _root.Changed += OnRootChanged;
        }

        UpdateIndicator(false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _attached = false;
        _mountVersion++;
        Selector.SelectionChanged -= OnSelectionChanged;
        LayoutUpdated -= OnLayoutUpdated;
        _settings.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
        if (_root is not null)
        {
            _root.Changed -= OnRootChanged;
            _root = null;
        }

        HideIndicator();
    }

    private void OnSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_synchronizing)
        {
            return;
        }

        SelectionUpdated();
        if (sender.SelectedItem is not null)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SelectionUpdated()
    {
        _animateNextLayout = _target is not null;
        if (_attached)
        {
            Selector.SelectedItem?.StartBringIntoView();
            UpdateIndicator(_animateNextLayout);
        }
    }

    private void OnLayoutUpdated(object? sender, object e) => UpdateIndicator(_animateNextLayout);

    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        HideIndicator();
        UpdateIndicator(false);
    }

    private void OnAnimationsEnabledChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args)
    {
        var version = _mountVersion;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_attached && version == _mountVersion)
            {
                HideIndicator();
                UpdateIndicator(false);
            }
        });
    }

    private void UpdateIndicator(bool animate)
    {
        var item = Selector.SelectedItem;
        if (!_attached || item is null || !Selector.Items.Contains(item) || item.ActualWidth <= 0 || item.ActualHeight <= 0)
        {
            HideIndicator();
            return;
        }

        var origin = item.TransformToVisual(Track).TransformPoint(default);
        var bounds = new Rect(origin.X, origin.Y, item.ActualWidth, item.ActualHeight);
        if (_target == bounds)
        {
            return;
        }

        if (_target is null)
        {
            // Initial selection may be beyond the viewport when titles overflow.
            item.StartBringIntoView();
        }

        var canAnimate = animate && _target is not null && _settings.AnimationsEnabled;
        // Capture the current presentation values before releasing the previous animation.
        var from = canAnimate
            ? new Rect(IndicatorTranslation.X, IndicatorTranslation.Y, Indicator.Width, Indicator.Height)
            : bounds;
        _transition?.Stop();
        _transition = null;
        _target = bounds;
        _animateNextLayout = false;
        IndicatorTranslation.X = bounds.X;
        IndicatorTranslation.Y = bounds.Y;
        Indicator.Width = bounds.Width;
        Indicator.Height = bounds.Height;
        Indicator.Visibility = Visibility.Visible;
        if (!canAnimate)
        {
            return;
        }

        // No completion callbacks: interrupted or unloaded transitions cannot write stale state.
        _transition = new Storyboard();
        AddAnimation(IndicatorTranslation, "X", from.X, bounds.X);
        AddAnimation(IndicatorTranslation, "Y", from.Y, bounds.Y);
        AddAnimation(Indicator, "Width", from.Width, bounds.Width);
        AddAnimation(Indicator, "Height", from.Height, bounds.Height);
        _transition.Begin();
    }

    private void AddAnimation(DependencyObject target, string path, double from, double to)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, path);
        _transition!.Children.Add(animation);
    }

    private void HideIndicator()
    {
        _transition?.Stop();
        _transition = null;
        _target = null;
        _animateNextLayout = false;
        Indicator.Visibility = Visibility.Collapsed;
    }
}
