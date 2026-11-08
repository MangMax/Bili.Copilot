// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// The window surface owned by <see cref="PlayerWindowPresentation"/>. Implementations
/// must stay on the window's UI context; only <c>WaitFor*</c> may suspend.
/// </summary>
internal interface IPlayerWindowSurface
{
    WindowRect Bounds { get; }

    WindowRect ClientBounds { get; }

    bool IsVisible { get; }

    bool IsMaximized { get; }

    WindowPresentationMode PresenterMode { get; }

    WindowRect GetWorkArea(WindowPoint point);

    void SetPresenter(WindowPresentationMode mode);

    void ApplyBounds(WindowRect bounds);

    void SetMaximized(bool maximized);

    /// <summary>
    /// Hides the window from composition without changing its visibility, activation or
    /// presenter state. Implementations may fall back to Hide/Show.
    /// </summary>
    void SetOccluded(bool occluded);

    void Show(bool activate);

    /// <summary>Waits until the presenter reports the requested mode and its bounds stop changing.</summary>
    Task<WindowRect> WaitForPresenterAsync(WindowPresentationMode mode, CancellationToken token);

    /// <summary>Waits until the requested mode and bounds have been composed.</summary>
    Task<WindowRect> WaitForBoundsAsync(WindowRect target, WindowPresentationMode mode, CancellationToken token);
}

/// <summary>Persisted ordinary geometry and compact origin; values only, no window access.</summary>
internal interface IPlayerWindowStateStore
{
    WindowRect? ReadOrdinaryBounds();

    bool ReadOrdinaryMaximized();

    void WriteOrdinaryBounds(WindowRect bounds, bool maximized);

    WindowPoint? ReadCompactPosition();

    void WriteCompactPosition(WindowPoint point);
}

/// <summary>
/// Owns every player-window mode transition: the target presenter, the complete target
/// geometry, visual protection, the completion criterion and persistence timing.
/// </summary>
/// <remarks>
/// A transition covers the window before the presenter changes and uncovers it only after
/// the requested presenter and bounds are composed. Presenter switches are synchronous in
/// Windows App SDK, but the last DWM frame is not; the surface confirms it. A superseded
/// transition never uncovers, moves or persists for the newer one, and no geometry is
/// written after the window becomes visible again.
/// </remarks>
internal sealed class PlayerWindowPresentation
{
    private readonly IPlayerWindowSurface _surface;
    private readonly IPlayerWindowStateStore _state;
    private readonly CompactOverlayRatio _ratio;
    private WindowPresentationMode _stableMode = WindowPresentationMode.Ordinary;
    private WindowPresentationMode _targetMode = WindowPresentationMode.Ordinary;
    private WindowRect? _ordinaryBounds;
    private bool _ordinaryMaximized;
    private long _generation;
    private CancellationTokenSource? _transitionCancellation;
    private Task _transition = Task.CompletedTask;
    private bool _closed;
    private bool _transitioning;
    private bool _occluded;
    private bool _visibleRequested;
    private bool _activateOnShow;
    private bool _hasShown;
    private bool _layoutDirty;

    public PlayerWindowPresentation(IPlayerWindowSurface surface, IPlayerWindowStateStore state,
        CompactOverlayRatio ratio)
    {
        _surface = surface;
        _state = state;
        _ratio = ratio;
        _ordinaryMaximized = state.ReadOrdinaryMaximized();
        _ratio.Changed += OnRatioChanged;
    }

    public bool IsChanging => _transitioning;

    /// <summary>
    /// Requests a logical mode. Repeated requests for the mode already in progress are
    /// coalesced; a different mode supersedes the transition and takes ownership of the
    /// existing protection.
    /// </summary>
    public Task RequestModeAsync(WindowPresentationMode mode)
    {
        if (_closed)
        {
            return Task.CompletedTask;
        }

        if (!_transitioning && _targetMode == mode && _stableMode == mode)
        {
            return Task.CompletedTask;
        }

        if (_transitioning && _targetMode == mode)
        {
            return _transition;
        }

        CaptureStableSource();
        _targetMode = mode;
        _visibleRequested |= _surface.IsVisible;
        var generation = ++_generation;
        _transitionCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _transitionCancellation = cancellation;
        _transition = TransitionAsync(mode, generation, cancellation);
        return _transition;
    }

    public void Show(bool restorePosition, bool activate)
    {
        if (_closed)
        {
            return;
        }

        _visibleRequested = true;
        _activateOnShow = activate;
        if (_transitioning)
        {
            return;
        }

        if (_stableMode == WindowPresentationMode.Ordinary && restorePosition && !_hasShown && !_surface.IsVisible)
        {
            _surface.ApplyBounds(GetOrdinaryTarget());
            if (_ordinaryMaximized)
            {
                _surface.SetMaximized(true);
            }
        }

        if (!_surface.IsVisible)
        {
            _surface.Show(activate);
        }

        _hasShown = true;
    }

    /// <summary>Reports a stable position/size/presenter change for persistence.</summary>
    public void GeometryChanged()
    {
        if (_closed || _transitioning)
        {
            return;
        }

        var presenter = _surface.PresenterMode;
        if (presenter == WindowPresentationMode.CompactOverlay && _stableMode == WindowPresentationMode.CompactOverlay)
        {
            var bounds = _surface.Bounds;
            if (IsUsableBounds(bounds))
            {
                _state.WriteCompactPosition(new WindowPoint(bounds.X, bounds.Y));
            }
        }
        else if (presenter == WindowPresentationMode.Ordinary && _stableMode == WindowPresentationMode.Ordinary)
        {
            var maximized = _surface.IsMaximized;
            var bounds = _surface.Bounds;
            _ordinaryMaximized = maximized;
            if (maximized)
            {
                _ordinaryBounds ??= _state.ReadOrdinaryBounds();
                _state.WriteOrdinaryBounds(_ordinaryBounds ?? bounds, true);
            }
            else if (IsUsableBounds(bounds))
            {
                _ordinaryBounds = bounds;
                _state.WriteOrdinaryBounds(bounds, false);
            }
        }
    }

    /// <summary>Restores a minimized/off-screen ordinary window on activation.</summary>
    public void RecoverOffscreenOrdinary()
    {
        if (_closed || _transitioning || _stableMode != WindowPresentationMode.Ordinary
            || _surface.PresenterMode != WindowPresentationMode.Ordinary)
        {
            return;
        }

        var bounds = _surface.Bounds;
        if (bounds.X >= -10000 && bounds.Y >= -10000)
        {
            return;
        }

        _surface.ApplyBounds(GetOrdinaryTarget());
    }

    public void Close()
    {
        if (_closed)
        {
            return;
        }

        GeometryChanged();
        _closed = true;
        _generation++;
        _transitionCancellation?.Cancel();
        _transitionCancellation = null;
        _ratio.Changed -= OnRatioChanged;
        _ratio.Dispose();
    }

    private async Task TransitionAsync(WindowPresentationMode mode, long generation,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        var startedVisible = _surface.IsVisible;
        var involvesCompact = mode == WindowPresentationMode.CompactOverlay
            || _stableMode == WindowPresentationMode.CompactOverlay;
        try
        {
            _transitioning = true;
            _layoutDirty = false;
            if (involvesCompact && startedVisible && !_occluded)
            {
                _surface.SetOccluded(true);
                _occluded = true;
            }

            if (_surface.PresenterMode != mode)
            {
                _surface.SetPresenter(mode);
            }

            if (mode == WindowPresentationMode.CompactOverlay)
            {
                var baseline = await _surface.WaitForPresenterAsync(mode, token);
                if (!IsCurrent(generation))
                {
                    return;
                }

                var target = ComputeCompactTarget(baseline);
                _surface.ApplyBounds(target);
                var actual = await _surface.WaitForBoundsAsync(target, mode, token);
                if (!IsCurrent(generation))
                {
                    return;
                }

                actual = await RecoverCompactAsync(actual, token);
                if (!IsCurrent(generation))
                {
                    return;
                }

                if (_layoutDirty && _ratio.Enabled && _ratio.Form > 0)
                {
                    _layoutDirty = false;
                    var refit = CompactOverlayLayout.Fit(new WindowPoint(actual.X, actual.Y), actual,
                        FrameWidth(actual), FrameHeight(actual), WorkArea(actual), _ratio.Form);
                    if (refit != actual)
                    {
                        _surface.ApplyBounds(refit);
                        actual = await _surface.WaitForBoundsAsync(refit, mode, token);
                        if (!IsCurrent(generation))
                        {
                            return;
                        }
                    }
                }

                _state.WriteCompactPosition(new WindowPoint(actual.X, actual.Y));
            }
            else if (mode == WindowPresentationMode.Ordinary)
            {
                if (_ordinaryMaximized)
                {
                    _surface.SetMaximized(false);
                }

                var target = GetOrdinaryTarget();
                _surface.ApplyBounds(target);
                await _surface.WaitForBoundsAsync(target, mode, token);
                if (!IsCurrent(generation))
                {
                    return;
                }

                if (_ordinaryMaximized)
                {
                    _surface.SetMaximized(true);
                }
            }
            else
            {
                await _surface.WaitForPresenterAsync(mode, token);
                if (!IsCurrent(generation))
                {
                    return;
                }
            }

            if (!IsCurrent(generation))
            {
                return;
            }

            _stableMode = _surface.PresenterMode;
            _transitioning = false;
            if (_occluded)
            {
                _surface.SetOccluded(false);
                _occluded = false;
            }

            if (!_surface.IsVisible && _visibleRequested)
            {
                _surface.Show(_activateOnShow);
                _hasShown = true;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer transition owns the window and any remaining protection.
        }
        catch
        {
            if (IsCurrent(generation))
            {
                _stableMode = _surface.PresenterMode;
                _transitioning = false;
                if (_occluded)
                {
                    _surface.SetOccluded(false);
                    _occluded = false;
                }
            }
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(_transitionCancellation, cancellation))
            {
                _transitionCancellation = null;
            }
        }
    }

    private WindowRect ComputeCompactTarget(WindowRect baseline)
    {
        var stored = _state.ReadCompactPosition();
        var anchor = stored ?? new WindowPoint(baseline.X, baseline.Y);
        var area = _surface.GetWorkArea(anchor);
        if (_ratio.Enabled && _ratio.Form > 0)
        {
            return CompactOverlayLayout.Fit(anchor, baseline, FrameWidth(baseline), FrameHeight(baseline), area, _ratio.Form);
        }

        var bounds = new WindowRect(anchor.X, anchor.Y, baseline.Width, baseline.Height);
        return CompactOverlayLayout.Recover(bounds, area);
    }

    private async Task<WindowRect> RecoverCompactAsync(WindowRect actual, CancellationToken token)
    {
        var recovered = CompactOverlayLayout.Recover(actual, WorkArea(actual));
        if (recovered == actual)
        {
            return actual;
        }

        _surface.ApplyBounds(recovered);
        return await _surface.WaitForBoundsAsync(recovered, WindowPresentationMode.CompactOverlay, token);
    }

    private WindowRect GetOrdinaryTarget()
        => _ordinaryBounds
            ?? _state.ReadOrdinaryBounds()
            ?? _surface.Bounds;

    private void CaptureStableSource()
    {
        if (_transitioning)
        {
            return;
        }

        if (_stableMode == WindowPresentationMode.CompactOverlay)
        {
            var bounds = _surface.Bounds;
            _state.WriteCompactPosition(new WindowPoint(bounds.X, bounds.Y));
        }
        else if (_stableMode == WindowPresentationMode.Ordinary)
        {
            if (!_hasShown && !_surface.IsVisible)
            {
                // The window was created but never presented (for example a default
                // compact start); the saved ordinary geometry is the stable source.
                _ordinaryMaximized = _state.ReadOrdinaryMaximized();
                _ordinaryBounds ??= _state.ReadOrdinaryBounds();
                return;
            }

            _ordinaryMaximized = _surface.IsMaximized;
            var bounds = _surface.Bounds;
            if (_ordinaryMaximized)
            {
                _ordinaryBounds ??= _state.ReadOrdinaryBounds();
            }
            else if (IsUsableBounds(bounds))
            {
                _ordinaryBounds = bounds;
            }
        }
    }

    private void OnRatioChanged(object? sender, EventArgs args)
    {
        if (_closed)
        {
            return;
        }

        if (_transitioning)
        {
            _layoutDirty = true;
            return;
        }

        if (_stableMode != WindowPresentationMode.CompactOverlay
            || _surface.PresenterMode != WindowPresentationMode.CompactOverlay
            || !_ratio.Enabled || _ratio.Form <= 0)
        {
            return;
        }

        var bounds = _surface.Bounds;
        var target = CompactOverlayLayout.Fit(new WindowPoint(bounds.X, bounds.Y), bounds,
            FrameWidth(bounds), FrameHeight(bounds), WorkArea(bounds), _ratio.Form);
        if (target == bounds)
        {
            return;
        }

        _surface.ApplyBounds(target);
        _state.WriteCompactPosition(new WindowPoint(target.X, target.Y));
    }

    private bool IsCurrent(long generation) => !_closed && generation == _generation;

    private static bool IsUsableBounds(WindowRect bounds)
        => bounds.X >= -10000 && bounds.Y >= -10000;

    private int FrameWidth(WindowRect bounds) => Math.Max(0, bounds.Width - _surface.ClientBounds.Width);

    private int FrameHeight(WindowRect bounds) => Math.Max(0, bounds.Height - _surface.ClientBounds.Height);

    private WindowRect WorkArea(WindowRect bounds) => _surface.GetWorkArea(new WindowPoint(bounds.X, bounds.Y));
}