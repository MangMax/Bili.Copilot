using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

internal sealed class FakeWindowSurface : IPlayerWindowSurface
{
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

    public bool Occluded { get; private set; }

    public List<string> Calls { get; } = new();

    public List<WindowRect> VisibleGeometry { get; } = new();

    public Func<WindowPresentationMode, WindowRect?>? PresenterBounds { get; set; }

    public Func<WindowRect, WindowPresentationMode, Task<WindowRect>>? WaitForBounds { get; set; }

    public Func<WindowPresentationMode, Task<WindowRect>>? WaitForPresenter { get; set; }

    public bool ThrowOnCompactApply { get; set; }

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
    }

    public void SetOccluded(bool occluded)
    {
        Calls.Add("occlude:" + occluded);
        Occluded = occluded;
    }

    public void Show(bool activate)
    {
        Calls.Add("show:" + activate);
        Visible = true;
    }

    public Task<WindowRect> WaitForPresenterAsync(WindowPresentationMode mode, CancellationToken token)
        => WaitForPresenter?.Invoke(mode) ?? Task.FromResult(Bounds);

    public Task<WindowRect> WaitForBoundsAsync(WindowRect target, WindowPresentationMode mode, CancellationToken token)
        => WaitForBounds?.Invoke(target, mode) ?? Task.FromResult(Bounds);

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

    public List<string> Writes { get; } = new();

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

internal static class PlayerWindowPresentationTestFactory
{
    public static (FakeWindowSurface Surface, FakeWindowStateStore State, CompactOverlayRatio Ratio,
        PlayerWindowPresentation Presentation) Create(bool enabled = true)
    {
        var surface = new FakeWindowSurface();
        var state = new FakeWindowStateStore();
        var preference = new CompactOverlayRatioPreference(() => enabled, _ => { });
        var ratio = new CompactOverlayRatio(preference);
        var presentation = new PlayerWindowPresentation(surface, state, ratio);
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
