// Copyright (c) Bili Copilot. All rights reserved.

using System.Numerics;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// Card cover with named overlay slots. The frame owns the cover, shade and
/// overlay positions; each card decides what goes into the slots.
/// </summary>
public sealed partial class CardMediaFrame : UserControl
{
    private readonly PointerEventHandler _pointerEnteredHandler;
    private readonly PointerEventHandler _pointerExitedHandler;
    private ButtonBase? _hoverSurface;
    private bool _isCoverZoomed;
    private bool _isMediaHovered;
    private bool _isCardHovered;

    public static readonly DependencyProperty CoverProperty = DependencyProperty.Register(
        nameof(Cover), typeof(Uri), typeof(CardMediaFrame), new PropertyMetadata(null));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(object), typeof(CardMediaFrame), new PropertyMetadata(null));

    public static readonly DependencyProperty TopStartProperty = DependencyProperty.Register(
        nameof(TopStart), typeof(object), typeof(CardMediaFrame), new PropertyMetadata(null));

    public static readonly DependencyProperty TopEndProperty = DependencyProperty.Register(
        nameof(TopEnd), typeof(object), typeof(CardMediaFrame), new PropertyMetadata(null));

    public static readonly DependencyProperty BottomStartProperty = DependencyProperty.Register(
        nameof(BottomStart), typeof(object), typeof(CardMediaFrame), new PropertyMetadata(null));

    public static readonly DependencyProperty BottomEndProperty = DependencyProperty.Register(
        nameof(BottomEnd), typeof(object), typeof(CardMediaFrame), new PropertyMetadata(null));

    /// <summary>
    /// Initializes a new instance of the <see cref="CardMediaFrame"/> class.
    /// </summary>
    public CardMediaFrame()
    {
        InitializeComponent();
        _pointerEnteredHandler = OnPointerEntered;
        _pointerExitedHandler = OnPointerExited;
        CoverClip.SizeChanged += (_, _) => UpdateCoverClip();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Gets or sets the cover image.
    /// </summary>
    public Uri? Cover
    {
        get => (Uri?)GetValue(CoverProperty);
        set => SetValue(CoverProperty, value);
    }

    /// <summary>
    /// Gets or sets the content shown below the cover, for cards without artwork.
    /// </summary>
    public object? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Gets or sets the top leading overlay slot.
    /// </summary>
    public object? TopStart
    {
        get => GetValue(TopStartProperty);
        set => SetValue(TopStartProperty, value);
    }

    /// <summary>
    /// Gets or sets the top trailing overlay slot.
    /// </summary>
    public object? TopEnd
    {
        get => GetValue(TopEndProperty);
        set => SetValue(TopEndProperty, value);
    }

    /// <summary>
    /// Gets or sets the bottom leading overlay slot, usually a row of stats.
    /// </summary>
    public object? BottomStart
    {
        get => GetValue(BottomStartProperty);
        set => SetValue(BottomStartProperty, value);
    }

    /// <summary>
    /// Gets or sets the bottom trailing overlay slot, usually a duration.
    /// </summary>
    public object? BottomEnd
    {
        get => GetValue(BottomEndProperty);
        set => SetValue(BottomEndProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        UpdateCoverClip();
        AddHandler(PointerEnteredEvent, _pointerEnteredHandler, true);
        AddHandler(PointerExitedEvent, _pointerExitedHandler, true);

        _hoverSurface = FindHoverSurface();
        if (_hoverSurface is null)
        {
            return;
        }

        _hoverSurface.AddHandler(PointerEnteredEvent, _pointerEnteredHandler, true);
        _hoverSurface.AddHandler(PointerExitedEvent, _pointerExitedHandler, true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_hoverSurface is not null)
        {
            _hoverSurface.RemoveHandler(PointerEnteredEvent, _pointerEnteredHandler);
            _hoverSurface.RemoveHandler(PointerExitedEvent, _pointerExitedHandler);
            _hoverSurface = null;
        }

        RemoveHandler(PointerEnteredEvent, _pointerEnteredHandler);
        RemoveHandler(PointerExitedEvent, _pointerExitedHandler);
        _isMediaHovered = false;
        _isCardHovered = false;
        _isCoverZoomed = false;
        CardCoverHover.Reset(CoverImage);
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (args.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            SetHovered(sender, true);
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs args)
    {
        if (args.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            SetHovered(sender, false);
        }
    }

    private void SetHovered(object sender, bool hovered)
    {
        if (ReferenceEquals(sender, _hoverSurface))
        {
            _isCardHovered = hovered;
        }
        else
        {
            _isMediaHovered = hovered;
        }

        ZoomCover(_isMediaHovered || _isCardHovered);
    }

    private void ZoomCover(bool zoom)
    {
        if (_isCoverZoomed == zoom)
        {
            return;
        }

        _isCoverZoomed = zoom;
        CardCoverHover.Animate(CoverImage, zoom);
    }

    /// <summary>
    /// Keeps the cover frame closed while the image is scaled by the hover motion.
    /// </summary>
    private void UpdateCoverClip()
    {
        if (CoverClip.XamlRoot is null || CoverClip.ActualWidth <= 0 || CoverClip.ActualHeight <= 0)
        {
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(CoverClip);
        var compositor = visual.Compositor;
        var geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.Size = new Vector2((float)CoverClip.ActualWidth, (float)CoverClip.ActualHeight);
        geometry.CornerRadius = new Vector2((float)CoverClip.CornerRadius.TopLeft);
        visual.Clip = compositor.CreateGeometricClip(geometry);
    }

    private ButtonBase? FindHoverSurface()
    {
        DependencyObject? current = VisualTreeHelper.GetParent(this);
        while (current is not null)
        {
            if (current is ButtonBase button)
            {
                return button;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
