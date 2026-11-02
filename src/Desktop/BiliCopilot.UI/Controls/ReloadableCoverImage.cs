// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.WinUIKernel.Share.Base;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace BiliCopilot.UI.Controls;

/// <summary>
/// Keeps the packaged image loader's lifecycle active when a recycled card is remounted.
/// </summary>
public abstract partial class ReloadableCoverImage : ImageExBase
{
    private bool _active;
    private bool _wasUnloaded;
    private CanvasAlphaMode _surfaceAlphaMode = CanvasAlphaMode.Ignore;

    protected ReloadableCoverImage()
    {
        // LayoutControlBase removes its own Loaded/Unloaded handlers on first unload.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnControlLoaded()
    {
        if (_active)
        {
            return;
        }

        _active = true;
        // ImageExBase clears its brush reference on unload, even when the template survives.
        base.OnApplyTemplate();
        if (_wasUnloaded && CanvasImageSource is null)
        {
            // Its composition sequence remains current, so TryInitialize otherwise skips
            // recreating the surface it discarded on unload. Retain the original alpha mode.
            CanvasImageSource = CreateSurface(_surfaceAlphaMode);
        }

        base.OnControlLoaded();
    }

    protected override void OnControlUnloaded()
    {
        if (!_active)
        {
            return;
        }

        _active = false;
        _wasUnloaded = true;
        base.OnControlUnloaded();
    }

    /// <summary>Recreates the aspect-ratio-adjusted surface used by Basic and Blur covers.</summary>
    protected void RecreateSurface()
    {
        _surfaceAlphaMode = CanvasAlphaMode.Premultiplied;
        CanvasImageSource = CreateSurface(_surfaceAlphaMode);
    }

    private CanvasImageSource CreateSurface(CanvasAlphaMode alphaMode)
        => new(CanvasDevice.GetSharedDevice(), (float)DecodeWidth, (float)DecodeHeight, 96, alphaMode);

    private void OnLoaded(object sender, RoutedEventArgs args) => OnControlLoaded();

    private void OnUnloaded(object sender, RoutedEventArgs args) => OnControlUnloaded();
}
