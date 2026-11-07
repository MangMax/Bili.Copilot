// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>Window and work-area coordinates, in physical pixels.</summary>
internal readonly record struct RatioWindowBounds(int X, int Y, int Width, int Height);

/// <summary>Shared, persisted preference for every playback window.</summary>
internal sealed class CompactOverlayRatioPreference(Func<bool?> read, Action<bool> write)
{
    public bool Enabled { get; private set; } = read() ?? true;

    public event EventHandler? Changed;

    public void SetEnabled(bool enabled)
    {
        if (Enabled == enabled)
        {
            return;
        }

        write(enabled);
        Enabled = enabled;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Coordinates media and setting changes at the window behavior boundary.</summary>
internal sealed class CompactOverlayRatioSession : IDisposable
{
    /// <summary>The only landscape compact form.</summary>
    public const double LandscapeRatio = 16d / 9d;

    /// <summary>The only portrait compact form.</summary>
    public const double PortraitRatio = 9d / 16d;

    private readonly CompactOverlayRatioPreference _preference;
    private readonly Func<RatioWindowBounds> _bounds;
    private readonly Func<RatioWindowBounds> _workArea;
    private readonly Action<RatioWindowBounds> _apply;
    private double _ratio;
    private bool _compact;
    private long _generation;
    private bool _closed;
    private long _presenterGeneration;

    public CompactOverlayRatioSession(CompactOverlayRatioPreference preference, Func<RatioWindowBounds> bounds,
        Func<RatioWindowBounds> workArea, Action<RatioWindowBounds> apply)
    {
        _preference = preference;
        _bounds = bounds;
        _workArea = workArea;
        _apply = apply;
        preference.Changed += OnPreferenceChanged;
    }

    public long BeginMedia()
    {
        _ratio = 0;
        return ++_generation;
    }

    public void UpdateMedia(long generation, double width, double height)
    {
        if (generation != _generation || !double.IsFinite(width) || !double.IsFinite(height)
            || width <= 0 || height <= 0 || !double.IsFinite(width / height) || width / height <= 0)
        {
            return;
        }

        // A compact window only offers a landscape (16:9) and a portrait (9:16) form.
        // Square, 4:3 or any other source ratio must never produce a near-square or
        // arbitrary window, because the overlay controls are laid out for these two
        // forms; the renderer letterboxes whatever the source aspect is.
        var ratio = SelectForm(width / height);
        if (_ratio == ratio)
        {
            return;
        }

        _ratio = ratio;
        Adapt();
    }

    /// <summary>
    /// Selects the compact form for a source ratio: square or wider content uses the
    /// landscape form, anything taller uses the portrait form.
    /// </summary>
    /// <param name="sourceRatio">Source width divided by height.</param>
    /// <returns><see cref="LandscapeRatio"/> or <see cref="PortraitRatio"/>.</returns>
    public static double SelectForm(double sourceRatio)
        => sourceRatio >= 1 ? LandscapeRatio : PortraitRatio;

    public void SetCompact(bool compact)
    {
        if (_compact == compact)
        {
            return;
        }

        _compact = compact;
        Adapt();
    }

    public async Task ChangePresenterAsync(Func<Task> change, Func<bool> isCompact)
    {
        if (_closed)
        {
            return;
        }

        var generation = ++_presenterGeneration;
        SetCompact(false);
        await change();
        if (!_closed && generation == _presenterGeneration)
        {
            SetCompact(isCompact());
        }
    }

    public void Dispose()
    {
        _closed = true;
        _preference.Changed -= OnPreferenceChanged;
    }

    /// <summary>
    /// Applies the known ratio immediately, before the compact flag is set and while the
    /// presenter is still settling. The presenter switch uses this so a portrait window
    /// never shows the presenter's default landscape size first.
    /// </summary>
    public void ApplyNow() => AdaptCore(force: true);

    private void OnPreferenceChanged(object? sender, EventArgs args) => Adapt();

    private void Adapt() => AdaptCore(force: false);

    private void AdaptCore(bool force)
    {
        if (_closed || (!force && !_compact) || !_preference.Enabled || _ratio <= 0)
        {
            return;
        }

        var bounds = _bounds();
        var area = _workArea();
        if (bounds.Width <= 0 || bounds.Height <= 0 || area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        var edge = Math.Max(bounds.Width, bounds.Height);
        var width = _ratio >= 1 ? edge : edge * _ratio;
        var height = _ratio >= 1 ? edge / _ratio : edge;
        var scale = Math.Min(1, Math.Min(area.Width / width, area.Height / height));
        var targetWidth = Math.Clamp((int)Math.Round(width * scale), 1, area.Width);
        var targetHeight = Math.Clamp((int)Math.Round(height * scale), 1, area.Height);
        // Keep the top-left anchor even when the user tucked the compact window over an
        // edge; only a fully hidden window is recovered into the work area.
        var x = CompactOverlayPlacement.RecoverAxis(bounds.X, targetWidth, area.X, area.Width);
        var y = CompactOverlayPlacement.RecoverAxis(bounds.Y, targetHeight, area.Y, area.Height);
        _apply(new(x, y, targetWidth, targetHeight));
    }
}
