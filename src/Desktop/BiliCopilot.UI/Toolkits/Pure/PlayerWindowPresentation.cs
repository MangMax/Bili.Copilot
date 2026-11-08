// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// The window surface owned by <see cref="PlayerWindowPresentation"/>. Implementations
/// must stay on the window's UI context; only <c>WaitForLayoutAsync</c> may suspend.
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
    /// presenter state, or reveals it again. The result is verified against the native
    /// window state; a failed reveal keeps the protection and must be retried, never
    /// reported as revealed.
    /// </summary>
    WindowProtectionResult SetOccluded(bool occluded);

    void Show(bool activate);

    /// <summary>
    /// Waits until <paramref name="target"/> is observed stable or the budget elapses. Never
    /// reads the window after it is gone or the request is cancelled.
    /// </summary>
    Task<WindowLayoutResult> WaitForLayoutAsync(WindowLayoutTarget target, TimeSpan budget,
        CancellationToken token);
}

/// <summary>The verified state of the window protection.</summary>
internal readonly record struct WindowProtectionResult(bool Verified, string? Diagnostic)
{
    public static WindowProtectionResult Ok { get; } = new(true, null);

    /// <summary>The protection is in place (or removed) but took a degraded path.</summary>
    public static WindowProtectionResult Degraded(string diagnostic) => new(true, diagnostic);

    public static WindowProtectionResult Failed(string diagnostic) => new(false, diagnostic);
}

internal enum WindowTransitionOutcome
{
    /// <summary>The requested stable state was reached and the window was revealed.</summary>
    Completed,

    /// <summary>The requested state was not reached; the failure is reported and logged.</summary>
    Failed,

    /// <summary>A newer request owns the window, or the window is closing.</summary>
    Cancelled,
}

/// <summary>How one mode transition ended, with non-fatal diagnostics when available.</summary>
internal readonly record struct WindowTransitionResult(WindowTransitionOutcome Outcome, string? Diagnostic = null)
{
    public static WindowTransitionResult Completed(string? diagnostic = null)
        => new(WindowTransitionOutcome.Completed, diagnostic);

    public static WindowTransitionResult Failed(string diagnostic)
        => new(WindowTransitionOutcome.Failed, diagnostic);

    public static WindowTransitionResult Cancelled { get; } = new(WindowTransitionOutcome.Cancelled);
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
/// the requested presenter, maximized state and frame are observed stable. Presenter
/// switches are synchronous in Windows App SDK, but the last DWM frame is not; the surface
/// confirms it. A superseded transition never uncovers, moves or persists for the newer one,
/// and no geometry is written after the window becomes visible again. Every transition
/// returns a result: a timeout or exception is logged, reported as failed, revealed so the
/// window cannot stay hidden, and left retryable.
/// </remarks>
internal sealed class PlayerWindowPresentation
{
    /// <summary>Longest one settlement phase may keep the window protected.</summary>
    private const int PhaseBudgetMilliseconds = 400;

    /// <summary>Longest the whole transition may keep the window protected.</summary>
    private const int TransitionBudgetMilliseconds = 1200;

    private readonly IPlayerWindowSurface _surface;
    private readonly IPlayerWindowStateStore _state;
    private readonly CompactOverlayRatio _ratio;
    private readonly ILogger _logger;
    private WindowPresentationMode? _stableMode = WindowPresentationMode.Ordinary;
    private WindowPresentationMode _targetMode = WindowPresentationMode.Ordinary;
    private WindowRect? _ordinaryBounds;
    private bool _ordinaryMaximized;
    private long _generation;
    private CancellationTokenSource? _transitionCancellation;
    private Task<WindowTransitionResult> _transition = Task.FromResult(WindowTransitionResult.Completed());
    private bool _closed;
    private bool _transitioning;
    private bool _occluded;
    private bool _visibleRequested;
    private bool _activateOnShow;
    private bool _hasShown;

    public PlayerWindowPresentation(IPlayerWindowSurface surface, IPlayerWindowStateStore state,
        CompactOverlayRatio ratio, ILogger? logger = null)
    {
        _surface = surface;
        _state = state;
        _ratio = ratio;
        _logger = logger ?? NullLogger.Instance;
        _ordinaryMaximized = state.ReadOrdinaryMaximized();
        _ratio.Changed += OnRatioChanged;
    }

    public bool IsChanging => _transitioning;

    /// <summary>
    /// Requests a logical mode. Repeated requests for the mode already in progress are
    /// coalesced; a different mode supersedes the transition and takes ownership of the
    /// existing protection. A failed transition leaves no stable mode behind, so the same
    /// target can be requested again and will run.
    /// </summary>
    public Task<WindowTransitionResult> RequestModeAsync(WindowPresentationMode mode)
    {
        if (_closed)
        {
            return Task.FromResult(WindowTransitionResult.Cancelled);
        }

        if (!_transitioning && !_occluded && _targetMode == mode && _stableMode == mode)
        {
            return Task.FromResult(WindowTransitionResult.Completed());
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

        if (_stableMode == WindowPresentationMode.Ordinary && restorePosition && !_hasShown
            && !_surface.IsVisible)
        {
            _surface.ApplyBounds(GetOrdinaryTarget());
            if (_ordinaryMaximized)
            {
                _surface.SetMaximized(true);
            }
        }

        if (!_surface.IsVisible)
        {
            try
            {
                _surface.Show(activate);
                _hasShown = true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "The player window could not be shown.");
            }
        }
    }

    /// <summary>Reports a stable position/size/presenter change for persistence.</summary>
    public void GeometryChanged()
    {
        if (_closed || _transitioning || _stableMode is null)
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

    private async Task<WindowTransitionResult> TransitionAsync(WindowPresentationMode mode, long generation,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        var deadline = Environment.TickCount64 + TransitionBudgetMilliseconds;
        var startedVisible = _surface.IsVisible;
        var involvesCompact = mode == WindowPresentationMode.CompactOverlay
            || _surface.PresenterMode == WindowPresentationMode.CompactOverlay
            || _stableMode == WindowPresentationMode.CompactOverlay;
        var diagnostic = default(string);
        var committed = false;
        try
        {
            _transitioning = true;
            if (involvesCompact && startedVisible && !_occluded)
            {
                var protection = Protect();
                if (!protection.Verified)
                {
                    diagnostic = Append(diagnostic, protection.Diagnostic);
                }
            }

            if (_surface.PresenterMode != mode)
            {
                _surface.SetPresenter(mode);
            }

            if (mode == WindowPresentationMode.CompactOverlay)
            {
                var baseline = await WaitAsync(WindowLayoutTarget.Presenter(mode), deadline, token);
                if (!IsCurrent(generation))
                {
                    return WindowTransitionResult.Cancelled;
                }

                if (!baseline.Succeeded)
                {
                    return Fail(generation, Phase("compact presenter", baseline), settled: false);
                }

                var actual = baseline.Sample.Bounds;
                while (true)
                {
                    // The latest media form owns the target: a change during a wait is
                    // picked up from the confirmed frame, so no input is lost.
                    var target = ComputeCompactTarget(actual);
                    if (WindowLayoutSettler.SameFrame(target, actual))
                    {
                        break;
                    }

                    _surface.ApplyBounds(target);
                    var settled = await WaitAsync(WindowLayoutTarget.Frame(mode, target), deadline, token);
                    if (!IsCurrent(generation))
                    {
                        return WindowTransitionResult.Cancelled;
                    }

                    if (!settled.Succeeded)
                    {
                        return Fail(generation, Phase("compact frame", settled), settled: false);
                    }

                    diagnostic = Append(diagnostic, settled.Diagnostic);
                    actual = settled.Sample.Bounds;
                    if (settled.Outcome == WindowLayoutOutcome.Constrained)
                    {
                        break;
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
                var restore = await WaitAsync(WindowLayoutTarget.Frame(mode, target), deadline, token);
                if (!IsCurrent(generation))
                {
                    return WindowTransitionResult.Cancelled;
                }

                if (!restore.Succeeded)
                {
                    return Fail(generation, Phase("ordinary frame", restore), settled: false);
                }

                diagnostic = Append(diagnostic, restore.Diagnostic);
                if (restore.Outcome == WindowLayoutOutcome.Constrained)
                {
                    // The system's own frame is the real restore rectangle.
                    _ordinaryBounds = restore.Sample.Bounds;
                }

                if (_ordinaryMaximized)
                {
                    _surface.SetMaximized(true);
                    var maximized = await WaitAsync(WindowLayoutTarget.MaximizedState(), deadline, token);
                    if (!IsCurrent(generation))
                    {
                        return WindowTransitionResult.Cancelled;
                    }

                    if (!maximized.Succeeded)
                    {
                        return Fail(generation, Phase("maximized frame", maximized), settled: false);
                    }
                }
            }
            else
            {
                var settled = await WaitAsync(WindowLayoutTarget.Presenter(mode), deadline, token);
                if (!IsCurrent(generation))
                {
                    return WindowTransitionResult.Cancelled;
                }

                if (!settled.Succeeded)
                {
                    return Fail(generation, Phase("full screen presenter", settled), settled: false);
                }
            }

            if (!IsCurrent(generation))
            {
                return WindowTransitionResult.Cancelled;
            }

            _stableMode = mode;
            _transitioning = false;
            committed = true;
            var reveal = Reveal();
            if (!reveal.Verified)
            {
                return Fail(generation, Append(diagnostic, reveal.Diagnostic)!, committed);
            }

            diagnostic = Append(diagnostic, reveal.Diagnostic);
            try
            {
                if (!_surface.IsVisible && _visibleRequested)
                {
                    _surface.Show(_activateOnShow);
                    _hasShown = true;
                }
            }
            catch (Exception exception)
            {
                return Fail(generation, "the player window could not be shown: " + exception.Message,
                    committed, exception);
            }

            return WindowTransitionResult.Completed(diagnostic);
        }
        catch (OperationCanceledException)
        {
            return WindowTransitionResult.Cancelled;
        }
        catch (Exception exception)
        {
            return Fail(generation, "the player window transition failed: " + exception.Message, committed,
                exception);
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(_transitionCancellation, cancellation))
            {
                _transitionCancellation = null;
            }

            if (IsCurrent(generation))
            {
                _transitioning = false;
            }
        }
    }

    private WindowTransitionResult Fail(long generation, string diagnostic, bool settled,
        Exception? exception = null)
    {
        if (!IsCurrent(generation))
        {
            return WindowTransitionResult.Cancelled;
        }

        _logger.LogError(exception, "Player window transition failed: {Diagnostic}", diagnostic);
        _transitioning = false;
        if (!settled)
        {
            // The requested state was never confirmed: every request, including the same
            // target, must run again instead of being coalesced as a no-op.
            _stableMode = null;
        }

        var reveal = Reveal();
        if (!reveal.Verified)
        {
            diagnostic = Append(diagnostic, reveal.Diagnostic) ?? diagnostic;
        }

        TryShow();
        return WindowTransitionResult.Failed(diagnostic);
    }

    private WindowProtectionResult Protect()
    {
        WindowProtectionResult protection;
        try
        {
            protection = _surface.SetOccluded(true);
        }
        catch (Exception exception)
        {
            protection = WindowProtectionResult.Failed(exception.Message);
        }

        _occluded = protection.Verified;
        if (!protection.Verified)
        {
            _logger.LogWarning("The player window could not be protected for the transition: {Diagnostic}",
                protection.Diagnostic);
        }
        else if (protection.Diagnostic is { } degraded)
        {
            _logger.LogWarning("Player window protection is degraded: {Diagnostic}", degraded);
        }

        return protection;
    }

    private WindowProtectionResult Reveal()
    {
        // Always ask the surface to release protection: it is idempotent and verifies the
        // native cloak, which also recovers a protection the coordinator did not record.
        WindowProtectionResult reveal;
        try
        {
            reveal = _surface.SetOccluded(false);
        }
        catch (Exception exception)
        {
            reveal = WindowProtectionResult.Failed(exception.Message);
        }

        if (reveal.Verified)
        {
            _occluded = false;
            if (reveal.Diagnostic is { } degraded)
            {
                _logger.LogWarning("Player window reveal is degraded: {Diagnostic}", degraded);
            }
        }
        else
        {
            _logger.LogError("The player window is still protected and cannot be revealed yet: {Diagnostic}",
                reveal.Diagnostic);
        }

        return reveal;
    }

    private void TryShow()
    {
        try
        {
            if (_surface.IsVisible || !_visibleRequested)
            {
                return;
            }

            _surface.Show(_activateOnShow);
            _hasShown = true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The player window could not be shown.");
        }
    }

    private Task<WindowLayoutResult> WaitAsync(WindowLayoutTarget target, long deadline, CancellationToken token)
    {
        var remaining = deadline - Environment.TickCount64;
        if (remaining <= 0)
        {
            return Task.FromResult(new WindowLayoutResult(WindowLayoutOutcome.TimedOut, default,
                "the transition budget elapsed"));
        }

        var budget = TimeSpan.FromMilliseconds(Math.Min(PhaseBudgetMilliseconds, remaining));
        return _surface.WaitForLayoutAsync(target, budget, token);
    }

    private WindowRect ComputeCompactTarget(WindowRect baseline)
    {
        var stored = _state.ReadCompactPosition();
        var anchor = stored ?? new WindowPoint(baseline.X, baseline.Y);
        var area = _surface.GetWorkArea(anchor);
        if (_ratio.Enabled && _ratio.Form > 0)
        {
            return CompactOverlayLayout.Fit(anchor, baseline, FrameWidth(baseline), FrameHeight(baseline), area,
                _ratio.Form);
        }

        var bounds = new WindowRect(anchor.X, anchor.Y, baseline.Width, baseline.Height);
        return CompactOverlayLayout.Recover(bounds, area);
    }

    private WindowRect GetOrdinaryTarget()
        => _ordinaryBounds
            ?? _state.ReadOrdinaryBounds()
            ?? _surface.Bounds;

    private void CaptureStableSource()
    {
        if (_transitioning || _stableMode is null)
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
            // The compact convergence loop reads the latest form before every apply and
            // before the commit, so the newest request always wins.
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

    private static string Phase(string phase, WindowLayoutResult result)
        => result.Diagnostic is { } diagnostic ? $"{phase}: {diagnostic}" : $"{phase} did not settle";

    private static string? Append(string? existing, string? addition)
    {
        if (string.IsNullOrEmpty(addition))
        {
            return existing;
        }

        return string.IsNullOrEmpty(existing) ? addition : $"{existing} / {addition}";
    }

    private static bool IsUsableBounds(WindowRect bounds)
        => bounds.X >= -10000 && bounds.Y >= -10000;

    private int FrameWidth(WindowRect bounds) => Math.Max(0, bounds.Width - _surface.ClientBounds.Width);

    private int FrameHeight(WindowRect bounds) => Math.Max(0, bounds.Height - _surface.ClientBounds.Height);

    private WindowRect WorkArea(WindowRect bounds) => _surface.GetWorkArea(new WindowPoint(bounds.X, bounds.Y));
}