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
            || width <= 0 || height <= 0 || !double.IsFinite(width / height) || width / height <= 0
            || _ratio == width / height)
        {
            return;
        }

        _ratio = width / height;
        Adapt();
    }

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

    private void OnPreferenceChanged(object? sender, EventArgs args) => Adapt();

    private void Adapt()
    {
        if (_closed || !_compact || !_preference.Enabled || _ratio <= 0)
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
        var x = (int)Math.Clamp((long)bounds.X, area.X, (long)area.X + area.Width - targetWidth);
        var y = (int)Math.Clamp((long)bounds.Y, area.Y, (long)area.Y + area.Height - targetHeight);
        _apply(new(x, y, targetWidth, targetHeight));
    }
}
