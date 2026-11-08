using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class PlayerWindowPresentationTests
{
    [Fact]
    public async Task EnteringCompactNeverMovesTheVisibleOrdinaryWindowAndOnlyExposesTheFinalFrame()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        ratio.UpdateMedia(ratio.BeginMedia(), 1920, 1080);

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);

        Assert.Equal(WindowPresentationMode.CompactOverlay, surface.Presenter);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);
        Assert.Empty(surface.VisibleGeometry);
        Assert.False(surface.Occluded);
        Assert.True(CallIndex(surface, "occlude:True") < CallIndex(surface, "presenter:CompactOverlay"));
        Assert.True(CallIndex(surface, "presenter:CompactOverlay") < IndexOfPrefix(surface, "bounds:"));
        Assert.True(IndexOfPrefix(surface, "bounds:") < CallIndex(surface, "occlude:False"));
        Assert.DoesNotContain(surface.Calls.Skip(CallIndex(surface, "occlude:False")), call => call.StartsWith("bounds:", StringComparison.Ordinal));
        Assert.Equal(new WindowPoint(300, 600), state.Compact);
    }

    [Fact]
    public async Task LeavingCompactRestoresTheExactOrdinaryGeometryAndKeepsOrdinarySettingsClean()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode switch
        {
            WindowPresentationMode.CompactOverlay => new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360),
            WindowPresentationMode.Ordinary => new WindowRect(100, 100, 1100, 700),
            _ => null,
        };

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        surface.Bounds = new(500, 500, 640, 360);
        presentation.GeometryChanged();
        await presentation.RequestModeAsync(WindowPresentationMode.Ordinary);

        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
        Assert.Equal(WindowPresentationMode.Ordinary, surface.Presenter);
        Assert.Equal(new WindowPoint(500, 500), state.Compact);
        Assert.Empty(surface.VisibleGeometry);
        Assert.DoesNotContain(state.Writes, write => write.StartsWith("ordinary:", StringComparison.Ordinal)
            && write.Contains("500,500", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompactOriginSurvivesCloseRecreationAndRestart()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        surface.WorkArea = new(-1920, 0, 1920, 1080);
        state.Compact = new(-900, 120);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        Assert.Equal(new WindowRect(-900, 120, 640, 360), surface.Bounds);

        surface.Bounds = new(-1000, 200, 640, 360);
        presentation.GeometryChanged();
        presentation.Close();

        var reopenedSurface = new FakeWindowSurface { Visible = false, WorkArea = new(-1920, 0, 1920, 1080) };
        reopenedSurface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(reopenedSurface.Bounds.X, reopenedSurface.Bounds.Y, 640, 360)
            : null;
        var reopenedState = new FakeWindowStateStore { Compact = state.Compact };
        using var reopenedRatio = new CompactOverlayRatio(new(() => true, _ => { }));
        var reopened = new PlayerWindowPresentation(reopenedSurface, reopenedState, reopenedRatio);

        await reopened.RequestModeAsync(WindowPresentationMode.CompactOverlay);

        Assert.Equal(new WindowRect(-1000, 200, 640, 360), reopenedSurface.Bounds);
    }

    [Fact]
    public async Task OlderTransitionCannotUncoverMoveOrPersistAfterANewerRequest()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode switch
        {
            WindowPresentationMode.CompactOverlay => new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360),
            WindowPresentationMode.Ordinary => new WindowRect(100, 100, 1100, 700),
            _ => null,
        };
        var blocked = new TaskCompletionSource<WindowRect>();
        var firstCompactWait = true;
        surface.WaitForBounds = (_, mode) =>
        {
            if (mode == WindowPresentationMode.CompactOverlay && firstCompactWait)
            {
                firstCompactWait = false;
                return blocked.Task;
            }

            return Task.FromResult(surface.Bounds);
        };

        var entering = presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        var leaving = presentation.RequestModeAsync(WindowPresentationMode.Ordinary);
        await leaving;
        blocked.SetResult(new WindowRect(300, 600, 640, 360));
        await entering;

        Assert.Equal(WindowPresentationMode.Ordinary, surface.Presenter);
        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
        Assert.Empty(surface.VisibleGeometry);
        Assert.Equal(1, surface.Calls.Count(call => call == "occlude:True"));
        Assert.Equal(1, surface.Calls.Count(call => call == "occlude:False"));
        Assert.Equal(new WindowPoint(300, 600), state.Compact);
    }

    [Fact]
    public async Task CloseDuringTransitionNeverShowsUncoversOrAppliesAgain()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        var blocked = new TaskCompletionSource<WindowRect>();
        surface.WaitForBounds = (_, _) => blocked.Task;

        var entering = presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        var callsAtClose = surface.Calls.Count;
        presentation.Close();
        blocked.SetResult(new WindowRect(300, 600, 640, 360));
        await entering;

        Assert.Equal(callsAtClose, surface.Calls.Count);
        Assert.DoesNotContain("show:True", surface.Calls);
        Assert.DoesNotContain("occlude:False", surface.Calls);
    }

    [Fact]
    public async Task HiddenEntryAppliesTheFinalGeometryAndShowOnlyPresentsIt()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        surface.Visible = false;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);

        Assert.DoesNotContain("show:True", surface.Calls);
        Assert.DoesNotContain("occlude:True", surface.Calls);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);

        presentation.Show(restorePosition: true, activate: false);

        Assert.Equal("show:False", surface.Calls[^1]);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);
    }

    [Fact]
    public async Task MaximizedOrdinaryWindowRestoresItsRestoreBoundsAndMaximizedState()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Ordinary = new(100, 100, 1100, 700);
        surface.Bounds = new(0, 0, 1920, 1040);
        surface.Maximized = true;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode switch
        {
            WindowPresentationMode.CompactOverlay => new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360),
            WindowPresentationMode.Ordinary => new WindowRect(0, 0, 1920, 1040),
            _ => null,
        };

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        await presentation.RequestModeAsync(WindowPresentationMode.Ordinary);

        Assert.True(surface.Maximized);
        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
        Assert.Contains("max:True", surface.Calls);
        Assert.Empty(surface.VisibleGeometry);
    }

    [Fact]
    public async Task DisabledRatioKeepsThePresenterSizeAndEnablingItRefitsTheKnownForm()
    {
        var surface = new FakeWindowSurface();
        var state = new FakeWindowStateStore { Compact = new(300, 600) };
        var enabled = false;
        var preference = new CompactOverlayRatioPreference(() => enabled, value => enabled = value);
        using var ratio = new CompactOverlayRatio(preference);
        var presentation = new PlayerWindowPresentation(surface, state, ratio);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        var generation = ratio.BeginMedia();
        ratio.UpdateMedia(generation, 1080, 1920);

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);

        preference.SetEnabled(true);
        Assert.Equal(new WindowRect(300, 600, 360, 640), surface.Bounds);

        surface.Bounds = new(300, 600, 500, 500);
        presentation.GeometryChanged();
        ratio.UpdateMedia(generation, 1080, 1920);
        Assert.Equal(new WindowRect(300, 600, 500, 500), surface.Bounds);

        ratio.UpdateMedia(generation, 1920, 1080);
        Assert.Equal(new WindowRect(300, 600, 500, 281), surface.Bounds);
    }

    [Fact]
    public async Task FullyHiddenCompactOriginIsRecoveredWithTheFittedSize()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(3000, 2000);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        ratio.UpdateMedia(ratio.BeginMedia(), 1920, 1080);

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);

        Assert.Equal(new WindowRect(1280, 680, 640, 360), surface.Bounds);
    }

    [Fact]
    public async Task PartiallyOverhangingCompactOriginIsPreserved()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(1800, 1000);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        ratio.UpdateMedia(ratio.BeginMedia(), 1920, 1080);

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);

        Assert.Equal(new WindowRect(1800, 1000, 640, 360), surface.Bounds);
    }

    [Fact]
    public async Task FullScreenTransitionsKeepTheSystemAnimationAndStillRestoreOrdinaryGeometry()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode switch
        {
            WindowPresentationMode.Ordinary => new WindowRect(100, 100, 1100, 700),
            WindowPresentationMode.CompactOverlay => new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360),
            _ => null,
        };

        await presentation.RequestModeAsync(WindowPresentationMode.FullScreen);
        Assert.DoesNotContain("occlude:True", surface.Calls);
        Assert.Equal(WindowPresentationMode.FullScreen, surface.Presenter);

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        Assert.Contains("occlude:True", surface.Calls);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);

        await presentation.RequestModeAsync(WindowPresentationMode.FullScreen);
        Assert.Equal(WindowPresentationMode.FullScreen, surface.Presenter);
        Assert.False(surface.Occluded);

        await presentation.RequestModeAsync(WindowPresentationMode.Ordinary);
        Assert.Equal(WindowPresentationMode.Ordinary, surface.Presenter);
        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
    }

    [Fact]
    public async Task GeometryFailureStillUncoversTheWindowAndLaterTransitionsWork()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        surface.ThrowOnCompactApply = true;

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);

        Assert.False(surface.Occluded);
        Assert.Equal(WindowPresentationMode.CompactOverlay, surface.Presenter);
        Assert.False(presentation.IsChanging);

        surface.ThrowOnCompactApply = false;
        await presentation.RequestModeAsync(WindowPresentationMode.Ordinary);
        Assert.Equal(WindowPresentationMode.Ordinary, surface.Presenter);
        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
    }

    [Fact]
    public async Task CompactMoveOnlyPersistsTheCompactOrigin()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        state.Writes.Clear();

        surface.Bounds = new(700, 300, 640, 360);
        presentation.GeometryChanged();

        Assert.Equal(["compact:700,300"], state.Writes);
        Assert.Equal(new WindowPoint(700, 300), state.Compact);
        Assert.Null(state.Ordinary);
    }

    [Fact]
    public void ShowNeverMovesAnAlreadyVisibleWindow()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Ordinary = new WindowRect(0, 0, 800, 600);
        var before = surface.Bounds;

        presentation.Show(restorePosition: true, activate: false);

        Assert.Equal(before, surface.Bounds);
        Assert.Empty(surface.VisibleGeometry);
    }

    [Fact]
    public async Task CompactBeforeFirstShowRestoresSavedOrdinaryGeometryAndMaximizedState()
    {
        var surface = new FakeWindowSurface { Visible = false, Bounds = new(0, 0, 800, 600) };
        var state = new FakeWindowStateStore { Ordinary = new WindowRect(100, 100, 1100, 700), Maximized = true };
        using var ratio = new CompactOverlayRatio(new(() => true, _ => { }));
        var presentation = new PlayerWindowPresentation(surface, state, ratio);
        surface.PresenterBounds = mode => mode == WindowPresentationMode.CompactOverlay
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;

        await presentation.RequestModeAsync(WindowPresentationMode.CompactOverlay);
        await presentation.RequestModeAsync(WindowPresentationMode.Ordinary);

        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
        Assert.True(surface.Maximized);
    }

    [Fact]
    public void InitialShowRestoresSavedOrdinaryGeometryAndMaximizedState()
    {
        var surface = new FakeWindowSurface { Visible = false };
        var state = new FakeWindowStateStore { Ordinary = new WindowRect(100, 100, 1100, 700), Maximized = true };
        using var ratio = new CompactOverlayRatio(new(() => true, _ => { }));
        var presentation = new PlayerWindowPresentation(surface, state, ratio);

        presentation.Show(restorePosition: true, activate: false);

        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
        Assert.True(surface.Maximized);
        Assert.Contains("max:True", surface.Calls);
    }

    [Fact]
    public void MinimizedSentinelGeometryNeverOverwritesTheOrdinarySnapshot()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Ordinary = new WindowRect(100, 100, 1100, 700);
        surface.Bounds = new(-32000, -32000, 160, 28);

        presentation.GeometryChanged();

        Assert.Equal(new WindowRect(100, 100, 1100, 700), state.Ordinary);
        Assert.Empty(state.Writes);

        surface.Bounds = new(240, 160, 1000, 650);
        presentation.GeometryChanged();
        Assert.Equal(new WindowRect(240, 160, 1000, 650), state.Ordinary);
    }

    [Fact]
    public void ActivationRecoversAMinimizedOrdinaryWindowInsteadOfPersistingTheSentinel()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Ordinary = new WindowRect(100, 100, 1100, 700);
        surface.Visible = false;
        surface.Bounds = new(-32000, -32000, 160, 28);

        presentation.RecoverOffscreenOrdinary();

        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
        Assert.Empty(surface.VisibleGeometry);
    }

    private static int CallIndex(FakeWindowSurface surface, string call)
        => surface.Calls.IndexOf(call);

    private static int IndexOfPrefix(FakeWindowSurface surface, string prefix)
        => surface.Calls.FindIndex(call => call.StartsWith(prefix, StringComparison.Ordinal));
}