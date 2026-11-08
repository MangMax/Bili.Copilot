// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

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

/// <summary>
/// The current media form and the global preference. This is a value provider only:
/// it never reads or writes window geometry and never changes a presenter.
/// </summary>
internal sealed class CompactOverlayRatio : IDisposable
{
    private readonly CompactOverlayRatioPreference _preference;
    private long _generation;
    private double _form;
    private bool _closed;

    public CompactOverlayRatio(CompactOverlayRatioPreference preference)
    {
        _preference = preference;
        _preference.Changed += OnPreferenceChanged;
    }

    /// <summary>Raised when the form or the global preference changes.</summary>
    public event EventHandler? Changed;

    public bool Enabled => !_closed && _preference.Enabled;

    /// <summary>Landscape or portrait form, or zero while the media is unknown.</summary>
    public double Form => _closed ? 0 : _form;

    /// <summary>Starts a media generation; late dimensions from older media are rejected.</summary>
    public long BeginMedia()
    {
        _form = 0;
        return ++_generation;
    }

    /// <summary>Accepts valid dimensions for the current media generation only.</summary>
    public void UpdateMedia(long generation, double width, double height)
    {
        if (_closed || generation != _generation || !double.IsFinite(width) || !double.IsFinite(height)
            || width <= 0 || height <= 0)
        {
            return;
        }

        var form = CompactOverlayLayout.SelectForm(width / height);
        if (_form == form)
        {
            return;
        }

        _form = form;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _preference.Changed -= OnPreferenceChanged;
    }

    private void OnPreferenceChanged(object? sender, EventArgs args)
        => Changed?.Invoke(this, EventArgs.Empty);
}
