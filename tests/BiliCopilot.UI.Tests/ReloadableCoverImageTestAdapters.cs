// Copyright (c) Bili Copilot. All rights reserved.

global using Microsoft.UI.Xaml;

// Headless stand-ins for the WinUIKernel image-control surface used by
// ReloadableCoverImage (#177). Only the template lifecycle contract is modeled.
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.UI.Xaml.Controls
{
    /// <summary>
    /// The template root panel ImageExBase looks up by name.
    /// </summary>
    public class Panel : Microsoft.UI.Xaml.UIElement
    {
        public object? Background { get; set; }
    }
}

namespace Microsoft.Graphics.Canvas
{
    public sealed class CanvasDevice
    {
        private static readonly CanvasDevice Shared = new();

        public static CanvasDevice GetSharedDevice() => Shared;
    }

    public enum CanvasAlphaMode
    {
        Ignore,
        Premultiplied,
        Straight,
    }
}

namespace Microsoft.Graphics.Canvas.UI.Xaml
{
    /// <summary>
    /// The retained image surface. Records the alpha mode chosen by the control.
    /// </summary>
    public sealed class CanvasImageSource
    {
        public CanvasImageSource(CanvasDevice device, float width, float height, float dpi, CanvasAlphaMode alphaMode)
        {
            Device = device;
            Width = width;
            Height = height;
            Dpi = dpi;
            AlphaMode = alphaMode;
        }

        public CanvasDevice Device { get; }

        public float Width { get; }

        public float Height { get; }

        public float Dpi { get; }

        public CanvasAlphaMode AlphaMode { get; }
    }
}

namespace Richasy.WinUIKernel.Share.Base
{
    /// <summary>
    /// Mirrors the packaged ImageExBase lifecycle:
    /// ApplyTemplate loads the template (and invokes OnApplyTemplate only when one is installed),
    /// OnApplyTemplate throws when "Root" is missing, and OnControlUnloaded clears the surface.
    /// </summary>
    public abstract class ImageExBase : Microsoft.UI.Xaml.FrameworkElement
    {
        private Microsoft.UI.Xaml.Controls.Panel? _rootPanel;
        private bool _templateApplied;

        public event Microsoft.UI.Xaml.RoutedEventHandler? Loaded;

        public event Microsoft.UI.Xaml.RoutedEventHandler? Unloaded;

        public int ApplyTemplateCalls { get; private set; }

        public int OnApplyTemplateCalls { get; private set; }

        public int OnControlLoadedCalls { get; private set; }

        public int OnControlUnloadedCalls { get; private set; }

        public double DecodeWidth { get; set; } = 1.0;

        public double DecodeHeight { get; set; } = 1.0;

        protected Microsoft.Graphics.Canvas.UI.Xaml.CanvasImageSource? CanvasImageSource { get; set; }

        /// <summary>Installs the control template, as a real Loaded/apply cycle would.</summary>
        public void InstallTemplate()
        {
            _rootPanel = new Microsoft.UI.Xaml.Controls.Panel();
            _templateApplied = false;
        }

        /// <summary>
        /// Mimics the observed WinUI contract: the return value reports whether this call
        /// applied the template, so an already-applied template returns false even though
        /// the part is present and usable.
        /// </summary>
        public bool ApplyTemplate()
        {
            ApplyTemplateCalls++;
            if (_rootPanel is null)
            {
                // No template: the real base returns false without invoking OnApplyTemplate.
                return false;
            }

            if (_templateApplied)
            {
                return false;
            }

            _templateApplied = true;
            OnApplyTemplate();
            return true;
        }

        public void Load() => Loaded?.Invoke(this, new Microsoft.UI.Xaml.RoutedEventArgs());

        public void Unload() => Unloaded?.Invoke(this, new Microsoft.UI.Xaml.RoutedEventArgs());

        protected object? GetTemplateChild(string name)
            => name == "Root" ? _rootPanel : null;

        protected virtual void OnApplyTemplate()
        {
            OnApplyTemplateCalls++;
            if (GetTemplateChild("Root") is not Microsoft.UI.Xaml.Controls.Panel)
            {
                throw new InvalidOperationException("TemplateRoot not found.");
            }
        }

        protected virtual void OnControlLoaded() => OnControlLoadedCalls++;

        protected virtual void OnControlUnloaded()
        {
            OnControlUnloadedCalls++;
            CanvasImageSource = null;
        }
    }
}
