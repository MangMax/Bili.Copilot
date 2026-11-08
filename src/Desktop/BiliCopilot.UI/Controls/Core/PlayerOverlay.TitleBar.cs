// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace BiliCopilot.UI.Controls.Core;

public sealed partial class PlayerOverlay
{
    private AppWindow? _titleWindow;
    private InputNonClientPointerSource? _titleInput;
    private XamlRoot? _titleRoot;
    private PlayerTitleBarLayout _titleLayout;
    private RectInt32[] _titleRegions = [];

    private void AttachTitleBar()
    {
        _titleWindow = ViewModel.Window?.GetWindow();
        _titleRoot = XamlRoot;
        if (_titleWindow is null || _titleRoot is null)
        {
            return;
        }

        _titleInput = InputNonClientPointerSource.GetForWindowId(_titleWindow.Id);
        _titleWindow.Changed += OnTitleWindowChanged;
        _titleRoot.Changed += OnTitleRootChanged;
        HeaderBar.LayoutUpdated += OnTitleLayoutUpdated;
        UpdateTitleBarLayout();
    }

    private void DetachTitleBar()
    {
        HeaderBar.LayoutUpdated -= OnTitleLayoutUpdated;
        if (_titleRoot is not null)
        {
            _titleRoot.Changed -= OnTitleRootChanged;
        }

        if (_titleWindow is not null)
        {
            _titleWindow.Changed -= OnTitleWindowChanged;
            if (ViewModel.Window is { IsClosed: false, IsDisposed: false })
            {
                _titleInput?.ClearRegionRects(NonClientRegionKind.Passthrough);
            }
        }

        _titleInput = null;
        _titleWindow = null;
        _titleRoot = null;
        _titleRegions = [];
    }

    private void OnTitleWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPresenterChange || args.DidVisibilityChange)
        {
            UpdateTitleBarLayout();
        }
    }

    private void OnTitleRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateTitleBarLayout();

    private void UpdateTitleBarLayout()
    {
        if (_titleWindow is null || _titleRoot is null || ViewModel.Window is not { IsClosed: false, IsDisposed: false })
        {
            return;
        }

        var fullScreen = _titleWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        var scale = _titleRoot.RasterizationScale;
        // Native size can shrink before the island's next arrange. Use the
        // smaller width so old XAML geometry cannot cover freshly moved buttons.
        var width = Math.Min(_titleRoot.Size.Width, _titleWindow.ClientSize.Width / scale);
        var layout = PlayerTitleBarLayout.Create(width, HeaderBar.Height, scale,
            fullScreen ? 0 : _titleWindow.TitleBar.LeftInset,
            fullScreen ? 0 : _titleWindow.TitleBar.RightInset,
            _titleWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay);
        if (layout == _titleLayout)
        {
            return;
        }

        // Invalidate old passthrough immediately. A resize must never leave old
        // app-button rectangles over the native minimize/maximize/close buttons.
        _titleInput!.ClearRegionRects(NonClientRegionKind.Passthrough);
        _titleRegions = [];
        _titleLayout = layout;
        HeaderLeftInset.Width = new GridLength(_titleLayout.Left);
        HeaderRightInset.Width = new GridLength(Math.Max(0, width - _titleLayout.Right));
        HeaderActions.Visibility = _titleLayout.ShowActions ? Visibility.Visible : Visibility.Collapsed;
        // LayoutUpdated publishes only the resulting arranged geometry, without
        // queued size snapshots, forced layout or fixed-duration delays.
    }

    private void OnTitleLayoutUpdated(object? sender, object args)
    {
        UpdateTitleBarLayout();
        UpdateTitleBarRegions();
    }

    private void UpdateTitleBarRegions()
    {
        if (_titleInput is null || _titleRoot is null || ViewModel.Window is not { IsClosed: false, IsDisposed: false })
        {
            return;
        }

        var regions = new List<RectInt32>();
        if (!ViewModel.IsControlsVisible)
        {
            // Let moving into the hidden header reveal controls, but never pass
            // native caption-button input through to the XAML island.
            AddRegion(_titleLayout.Left, 0, _titleLayout.Right - _titleLayout.Left, _titleLayout.Height);
        }
        else
        {
            AddElement(HeaderNavigation);
            AddElement(HeaderActions);
        }

        if (!_titleRegions.SequenceEqual(regions))
        {
            _titleRegions = regions.ToArray();
            if (_titleRegions.Length == 0)
            {
                _titleInput.ClearRegionRects(NonClientRegionKind.Passthrough);
            }
            else
            {
                _titleInput.SetRegionRects(NonClientRegionKind.Passthrough, _titleRegions);
            }
        }

        void AddElement(FrameworkElement element)
        {
            if (element.Visibility != Visibility.Visible)
            {
                return;
            }

            var bounds = element.TransformToVisual(this).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            AddRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        void AddRegion(double x, double y, double width, double height)
        {
            var bounds = _titleLayout.Clip(x, y, width, height);
            var scale = _titleRoot.RasterizationScale;
            // Round inward so fractional DPI cannot include a native caption pixel.
            var left = (int)Math.Ceiling(bounds.X * scale);
            var top = (int)Math.Ceiling(bounds.Y * scale);
            var right = (int)Math.Floor((bounds.X + bounds.Width) * scale);
            var bottom = (int)Math.Floor((bounds.Y + bounds.Height) * scale);
            if (right > left && bottom > top)
            {
                regions.Add(new RectInt32(left, top, right - left, bottom - top));
            }
        }
    }
}
