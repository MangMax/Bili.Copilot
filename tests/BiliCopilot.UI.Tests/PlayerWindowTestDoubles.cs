using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;

namespace BiliCopilot.UI.Tests;

internal sealed class FakeWindowSurface : IPlayerWindowSurface
{
    private WindowRect? _pendingMaximize;

    public WindowRect Bounds { get; set; } = new(100, 100, 1100, 700);

    public int FrameWidth { get; set; }

    public int FrameHeight { get; set; }

    public WindowRect ClientBounds
        => new(Bounds.X, Bounds.Y, Math.Max(1, Bounds.Width - FrameWidth), Math.Max(1, Bounds.Height - FrameHeight));

    public bool Visible { get; set; } = true;

    public bool IsVisible => Visible;

    public bool Maximized { get; set; }

    public bool IsMaximized => Maximized;

    public WindowPresentationMode Presenter { get; set; } = WindowPresentationMode.Ordinary;

    public WindowPresentationMode PresenterMode => Presenter;

    public WindowRect WorkArea { get; set; } = new(0, 0, 1920, 1040);

    /// <summary>The protection the surface actually holds; a failed reveal keeps it on.</summary>
    public bool Occluded { get; private set; }

    public List<string> Calls { get; } = [];

    public List<WindowRect> VisibleGeometry { get; } = [];

    public WindowLayoutSample Sample => new(Presenter, Bounds, Maximized);

    public Func<WindowPresentationMode, WindowRect?>? PresenterBounds { get; set; }

    public Func<WindowLayoutTarget, TimeSpan, CancellationToken, Task<WindowLayoutResult>>? WaitForLayout { get; set; }

    public Func<bool, WindowProtectionResult>? Protection { get; set; }

    /// <summary>A maximized frame the system applies on a later composition, not in the call.</summary>
    public WindowRect? DeferredMaximizedBounds { get; set; }

    public bool ThrowOnCompactApply { get; set; }

    public bool ThrowOnShow { get; set; }

    public WindowRect GetWorkArea(WindowPoint point) => WorkArea;

    public void SetPresenter(WindowPresentationMode mode)
    {
        Calls.Add("presenter:" + mode);
        Presenter = mode;
        if (PresenterBounds?.Invoke(mode) is { } bounds)
        {
            SetBounds(bounds);
        }
    }

    public void ApplyBounds(WindowRect bounds)
    {
        Calls.Add("bounds:" + bounds);
        if (ThrowOnCompactApply && Presenter == WindowPresentationMode.CompactOverlay)
        {
            throw new InvalidOperationException("presenter rejected geometry");
        }

        SetBounds(bounds);
    }

    public void SetMaximized(bool maximized)
    {
        Calls.Add("max:" + maximized);
        Maximized = maximized;
        if (maximized && DeferredMaximizedBounds is { } bounds)
        {
            _pendingMaximize = bounds;
        }
    }

    public WindowProtectionResult SetOccluded(bool occluded)
    {
        Calls.Add("occlude:" + occluded);
        var result = Protection?.Invoke(occluded) ?? WindowProtectionResult.Ok;
        if (result.Verified)
        {
            Occluded = occluded;
        }

        if (!occluded)
        {
            FlushPendingMaximize();
        }

        return result;
    }

    public void Show(bool activate)
    {
        Calls.Add("show:" + activate);
        if (ThrowOnShow)
        {
            throw new InvalidOperationException("show failed");
        }

        Visible = true;
    }

    public Task<WindowLayoutResult> WaitForLayoutAsync(WindowLayoutTarget target, TimeSpan budget,
        CancellationToken token)
    {
        Calls.Add($"wait:{target.Mode}:{target.Bounds?.ToString() ?? "any"}:{target.Maximized}");
        FlushPendingMaximize();
        return WaitForLayout?.Invoke(target, budget, token) ?? Task.FromResult(Settle(target));
    }

    /// <summary>Simulates a window manager that applied every request synchronously.</summary>
    public WindowLayoutResult Settle(WindowLayoutTarget target)
    {
        if (Presenter != target.Mode
            || (target.Mode == WindowPresentationMode.Ordinary && Maximized != target.Maximized))
        {
            return new(WindowLayoutOutcome.TimedOut, Sample, "the requested presenter did not settle");
        }

        return target.Bounds is { } expected && !WindowLayoutSettler.SameFrame(Bounds, expected)
            ? new(WindowLayoutOutcome.Constrained, Sample, "the system kept its own frame")
            : new(WindowLayoutOutcome.Confirmed, Sample);
    }

    private void FlushPendingMaximize()
    {
        if (_pendingMaximize is not { } bounds)
        {
            return;
        }

        _pendingMaximize = null;
        SetBounds(bounds);
    }

    private void SetBounds(WindowRect bounds)
    {
        Bounds = bounds;
        if (Visible && !Occluded)
        {
            VisibleGeometry.Add(bounds);
        }
    }
}

internal sealed class FakeWindowStateStore : IPlayerWindowStateStore
{
    public WindowRect? Ordinary { get; set; }

    public bool Maximized { get; set; }

    public WindowPoint? Compact { get; set; }

    public List<string> Writes { get; } = [];

    public WindowRect? ReadOrdinaryBounds() => Ordinary;

    public bool ReadOrdinaryMaximized() => Maximized;

    public void WriteOrdinaryBounds(WindowRect bounds, bool maximized)
    {
        Writes.Add($"ordinary:{bounds}:{maximized}");
        if (!maximized)
        {
            Ordinary = bounds;
        }

        Maximized = maximized;
    }

    public WindowPoint? ReadCompactPosition() => Compact;

    public void WriteCompactPosition(WindowPoint point)
    {
        Writes.Add("compact:" + point.ToSetting());
        Compact = point;
    }
}

internal sealed class RecordingLogger : ILogger
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception), exception));
}

internal static class PlayerWindowPresentationTestFactory
{
    public static (FakeWindowSurface Surface, FakeWindowStateStore State, CompactOverlayRatio Ratio,
        PlayerWindowPresentation Presentation) Create(bool enabled = true, RecordingLogger? logger = null)
    {
        var surface = new FakeWindowSurface();
        var state = new FakeWindowStateStore();
        var preference = new CompactOverlayRatioPreference(() => enabled, _ => { });
        var ratio = new CompactOverlayRatio(preference);
        var presentation = new PlayerWindowPresentation(surface, state, ratio, logger);
        return (surface, state, ratio, presentation);
    }
}

internal sealed class RatioWindowHarness : IDisposable
{
    public RatioWindowHarness(bool enabled = true)
    {
        Surface.Bounds = new WindowRect(100, 80, 640, 400);
        State.Compact = new WindowPoint(100, 80);
        Preference = new CompactOverlayRatioPreference(() => enabled, _ => { });
        Ratio = new CompactOverlayRatio(Preference);
        Presentation = new PlayerWindowPresentation(Surface, State, Ratio);
    }

    public FakeWindowSurface Surface { get; } = new();

    public FakeWindowStateStore State { get; } = new();

    public CompactOverlayRatioPreference Preference { get; }

    public CompactOverlayRatio Ratio { get; }

    public PlayerWindowPresentation Presentation { get; }

    public WindowRect Bounds
    {
        get => Surface.Bounds;
        set
        {
            Surface.Bounds = value;
            Presentation.GeometryChanged();
        }
    }

    public static async Task<RatioWindowHarness> CreateCompactAsync(bool enabled = true)
    {
        var window = new RatioWindowHarness(enabled);
        await window.Presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        return window;
    }

    public void Dispose() => Presentation.Close();
}
