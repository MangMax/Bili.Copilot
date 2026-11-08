using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;

namespace BiliCopilot.UI.Tests;

public sealed class PlayerWindowPresentationTests
{
    private const WindowPresentationMode Compact = WindowPresentationMode.CompactOverlay;
    private const WindowPresentationMode Ordinary = WindowPresentationMode.Ordinary;

    [Fact]
    public async Task EnteringCompactNeverMovesTheVisibleOrdinaryWindowAndOnlyExposesTheFinalFrame()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        ratio.UpdateMedia(ratio.BeginMedia(), 1920, 1080);

        await presentation.RequestModeAsync(Compact);

        Assert.Equal(Compact, surface.Presenter);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);
        Assert.Empty(surface.VisibleGeometry);
        Assert.False(surface.Occluded);
        Assert.True(CallIndex(surface, "occlude:True") < CallIndex(surface, "presenter:CompactOverlay"));
        Assert.True(CallIndex(surface, "presenter:CompactOverlay") < IndexOfPrefix(surface, "bounds:"));
        Assert.True(IndexOfPrefix(surface, "bounds:") < CallIndex(surface, "occlude:False"));
        Assert.DoesNotContain(surface.Calls.Skip(CallIndex(surface, "occlude:False")),
            call => call.StartsWith("bounds:", StringComparison.Ordinal));
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
            Compact => new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360),
            Ordinary => new WindowRect(100, 100, 1100, 700),
            _ => null,
        };

        await presentation.RequestModeAsync(Compact);
        surface.Bounds = new(500, 500, 640, 360);
        presentation.GeometryChanged();
        await presentation.RequestModeAsync(Ordinary);

        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
        Assert.Equal(Ordinary, surface.Presenter);
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
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;

        await presentation.RequestModeAsync(Compact);
        Assert.Equal(new WindowRect(-900, 120, 640, 360), surface.Bounds);

        surface.Bounds = new(-1000, 200, 640, 360);
        presentation.GeometryChanged();
        presentation.Close();

        var reopenedSurface = new FakeWindowSurface { Visible = false, WorkArea = new(-1920, 0, 1920, 1080) };
        reopenedSurface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(reopenedSurface.Bounds.X, reopenedSurface.Bounds.Y, 640, 360)
            : null;
        var reopenedState = new FakeWindowStateStore { Compact = state.Compact };
        using var reopenedRatio = new CompactOverlayRatio(new(() => true, _ => { }));
        var reopened = new PlayerWindowPresentation(reopenedSurface, reopenedState, reopenedRatio);

        await reopened.RequestModeAsync(Compact);

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
            Compact => new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360),
            Ordinary => new WindowRect(100, 100, 1100, 700),
            _ => null,
        };
        var blocked = new TaskCompletionSource<WindowLayoutResult>();
        var firstCompactFrame = true;
        surface.WaitForLayout = (target, _, _) =>
        {
            if (target.Mode == Compact && target.Bounds is not null && firstCompactFrame)
            {
                firstCompactFrame = false;
                return blocked.Task;
            }

            return Task.FromResult(surface.Settle(target));
        };

        var entering = presentation.RequestModeAsync(Compact);
        var leaving = presentation.RequestModeAsync(Ordinary);
        await leaving;
        blocked.SetResult(new WindowLayoutResult(WindowLayoutOutcome.Confirmed,
            new WindowLayoutSample(Compact, new WindowRect(300, 600, 640, 360), false)));
        var superseded = await entering;

        Assert.Equal(WindowTransitionOutcome.Cancelled, superseded.Outcome);
        Assert.Equal(Ordinary, surface.Presenter);
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
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        var blocked = new TaskCompletionSource<WindowLayoutResult>();
        surface.WaitForLayout = (_, _, _) => blocked.Task;

        var entering = presentation.RequestModeAsync(Compact);
        var callsAtClose = surface.Calls.Count;
        presentation.Close();
        blocked.SetResult(new WindowLayoutResult(WindowLayoutOutcome.Confirmed,
            new WindowLayoutSample(Compact, new WindowRect(300, 600, 640, 360), false)));
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
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;

        await presentation.RequestModeAsync(Compact);

        Assert.DoesNotContain("show:True", surface.Calls);
        Assert.DoesNotContain("occlude:True", surface.Calls);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);

        presentation.Show(restorePosition: true, activate: false);

        Assert.Equal("show:False", surface.Calls[^1]);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);
    }

    [Fact]
    public async Task MaximizedRestoreOnlyRevealsAfterTheMaximizedFrameIsComposed()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Ordinary = new(100, 100, 1100, 700);
        state.Maximized = true;
        surface.Bounds = new(0, 0, 1920, 1040);
        surface.Maximized = true;
        surface.DeferredMaximizedBounds = new(0, 0, 1920, 1040);
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode switch
        {
            Compact => new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360),
            Ordinary => new WindowRect(0, 0, 1920, 1040),
            _ => null,
        };

        await presentation.RequestModeAsync(Compact);
        var result = await presentation.RequestModeAsync(Ordinary);

        Assert.Equal(WindowTransitionOutcome.Completed, result.Outcome);
        Assert.True(surface.Maximized);
        Assert.Equal(new WindowRect(0, 0, 1920, 1040), surface.Bounds);
        Assert.Equal(new WindowRect(100, 100, 1100, 700), state.Ordinary);
        Assert.Empty(surface.VisibleGeometry);
        var restore = CallIndex(surface, "bounds:100,100,1100,700");
        var maximize = CallIndex(surface, "max:True");
        var maximizedFrame = surface.Calls.FindIndex(call => call.StartsWith("wait:Ordinary:any:True", StringComparison.Ordinal));
        Assert.True(restore < maximize);
        Assert.True(maximize < maximizedFrame);
        Assert.True(maximizedFrame < surface.Calls.FindLastIndex(call => call == "occlude:False"));
    }

    [Fact]
    public async Task SettlingTimeoutRevealsReportsFailureAndAllowsTheSameTargetAgain()
    {
        var logger = new RecordingLogger();
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create(logger: logger);
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        var timedOut = false;
        surface.WaitForLayout = (target, _, _) =>
        {
            if (!timedOut && target.Bounds is not null)
            {
                timedOut = true;
                return Task.FromResult(new WindowLayoutResult(WindowLayoutOutcome.TimedOut, surface.Sample,
                    "the compact frame never settled"));
            }

            return Task.FromResult(surface.Settle(target));
        };

        var failed = await presentation.RequestModeAsync(Compact);

        Assert.Equal(WindowTransitionOutcome.Failed, failed.Outcome);
        Assert.Contains("never settled", failed.Diagnostic);
        Assert.False(surface.Occluded);
        Assert.False(presentation.IsChanging);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.DoesNotContain(state.Writes, write => write.StartsWith("compact:", StringComparison.Ordinal));

        state.Writes.Clear();
        surface.Bounds = new(500, 500, 640, 360);
        presentation.GeometryChanged();
        Assert.Empty(state.Writes);

        var retry = await presentation.RequestModeAsync(Compact);

        Assert.Equal(WindowTransitionOutcome.Completed, retry.Outcome);
        Assert.Equal(Compact, surface.Presenter);
        Assert.False(surface.Occluded);
        // The retry really ran the compact path instead of being coalesced as a no-op.
        Assert.Contains("compact:300,600", state.Writes);
    }

    [Fact]
    public async Task SystemConstrainedFrameIsAcceptedAsTheRealGeometryWithADiagnostic()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        var constrained = false;
        surface.WaitForLayout = (target, _, _) =>
        {
            if (target.Bounds is not null && !constrained)
            {
                constrained = true;
                surface.Bounds = new WindowRect(300, 600, 400, 400);
                return Task.FromResult(new WindowLayoutResult(WindowLayoutOutcome.Constrained, surface.Sample,
                    "the window manager kept 400x400"));
            }

            return Task.FromResult(surface.Settle(target));
        };

        var result = await presentation.RequestModeAsync(Compact);

        Assert.Equal(WindowTransitionOutcome.Completed, result.Outcome);
        Assert.Contains("400x400", result.Diagnostic);
        Assert.Equal(new WindowRect(300, 600, 400, 400), surface.Bounds);
        Assert.Equal(new WindowPoint(300, 600), state.Compact);
        Assert.False(surface.Occluded);
        Assert.Equal(1, surface.Calls.Count(call => call.StartsWith("bounds:", StringComparison.Ordinal)));
        var reveal = surface.Calls.FindLastIndex(call => call == "occlude:False");
        Assert.DoesNotContain(surface.Calls.Skip(reveal + 1), call => call.StartsWith("bounds:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RevealFailureIsReportedKeepsProtectionAndTheSameRequestRetries()
    {
        var logger = new RecordingLogger();
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create(logger: logger);
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        surface.Protection = occluded => occluded
            ? WindowProtectionResult.Ok
            : WindowProtectionResult.Failed("DWMWA_CLOAK did not clear");

        var failed = await presentation.RequestModeAsync(Compact);

        Assert.Equal(WindowTransitionOutcome.Failed, failed.Outcome);
        Assert.Contains("DWMWA_CLOAK", failed.Diagnostic);
        Assert.True(surface.Occluded);
        Assert.False(presentation.IsChanging);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);

        surface.Protection = _ => WindowProtectionResult.Ok;
        var retry = await presentation.RequestModeAsync(Compact);

        Assert.Equal(WindowTransitionOutcome.Completed, retry.Outcome);
        Assert.False(surface.Occluded);
    }

    [Fact]
    public async Task InitialShowFailureIsReportedAndALaterShowPresentsTheWindow()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        surface.Visible = false;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        var release = new TaskCompletionSource();
        surface.WaitForLayout = async (target, _, _) =>
        {
            await release.Task;
            return surface.Settle(target);
        };

        var entering = presentation.RequestModeAsync(Compact);
        presentation.Show(restorePosition: true, activate: false);
        surface.ThrowOnShow = true;
        release.SetResult();
        var failed = await entering;

        Assert.Equal(WindowTransitionOutcome.Failed, failed.Outcome);
        Assert.Contains("show", failed.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.False(surface.Visible);
        Assert.False(presentation.IsChanging);

        surface.ThrowOnShow = false;
        presentation.Show(restorePosition: false, activate: false);

        Assert.True(surface.Visible);
    }

    [Fact]
    public async Task RatioChangeDuringTheRefitConvergesToTheLatestForm()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        var media = ratio.BeginMedia();
        ratio.UpdateMedia(media, 1920, 1080);
        var frameWaits = 0;
        surface.WaitForLayout = (target, _, _) =>
        {
            if (target.Bounds is not null)
            {
                frameWaits++;
                if (frameWaits == 1)
                {
                    ratio.UpdateMedia(media, 1080, 1920);
                }
                else if (frameWaits == 2)
                {
                    ratio.UpdateMedia(media, 1920, 1080);
                }
            }

            return Task.FromResult(surface.Settle(target));
        };

        var result = await presentation.RequestModeAsync(Compact);

        Assert.Equal(WindowTransitionOutcome.Completed, result.Outcome);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);
        Assert.True(frameWaits >= 3);
        var reveal = surface.Calls.FindLastIndex(call => call == "occlude:False");
        Assert.DoesNotContain(surface.Calls.Skip(reveal + 1), call => call.StartsWith("bounds:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnprotectableWindowCompletesTheModeButReportsTheDegradedProtection()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        surface.Protection = occluded => occluded
            ? WindowProtectionResult.Failed("neither DWMWA_CLOAK nor Hide protected the window")
            : WindowProtectionResult.Ok;

        var result = await presentation.RequestModeAsync(Compact);

        Assert.Equal(WindowTransitionOutcome.Completed, result.Outcome);
        Assert.Contains("protect", result.Diagnostic!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Compact, surface.Presenter);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);
        Assert.False(surface.Occluded);
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
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        var generation = ratio.BeginMedia();
        ratio.UpdateMedia(generation, 1080, 1920);

        await presentation.RequestModeAsync(Compact);
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
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        ratio.UpdateMedia(ratio.BeginMedia(), 1920, 1080);

        await presentation.RequestModeAsync(Compact);

        Assert.Equal(new WindowRect(1280, 680, 640, 360), surface.Bounds);
    }

    [Fact]
    public async Task PartiallyOverhangingCompactOriginIsPreserved()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(1800, 1000);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        ratio.UpdateMedia(ratio.BeginMedia(), 1920, 1080);

        await presentation.RequestModeAsync(Compact);

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
            Ordinary => new WindowRect(100, 100, 1100, 700),
            Compact => new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360),
            _ => null,
        };

        await presentation.RequestModeAsync(WindowPresentationMode.FullScreen);
        Assert.DoesNotContain("occlude:True", surface.Calls);
        Assert.Equal(WindowPresentationMode.FullScreen, surface.Presenter);

        await presentation.RequestModeAsync(Compact);
        Assert.Contains("occlude:True", surface.Calls);
        Assert.Equal(new WindowRect(300, 600, 640, 360), surface.Bounds);

        await presentation.RequestModeAsync(WindowPresentationMode.FullScreen);
        Assert.Equal(WindowPresentationMode.FullScreen, surface.Presenter);
        Assert.False(surface.Occluded);

        await presentation.RequestModeAsync(Ordinary);
        Assert.Equal(Ordinary, surface.Presenter);
        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
    }

    [Fact]
    public async Task GeometryFailureStillUncoversTheWindowAndLaterTransitionsWork()
    {
        var logger = new RecordingLogger();
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create(logger: logger);
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        surface.ThrowOnCompactApply = true;

        var failed = await presentation.RequestModeAsync(Compact);

        Assert.Equal(WindowTransitionOutcome.Failed, failed.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(failed.Diagnostic));
        Assert.False(surface.Occluded);
        Assert.Equal(Compact, surface.Presenter);
        Assert.False(presentation.IsChanging);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is not null);

        surface.ThrowOnCompactApply = false;
        await presentation.RequestModeAsync(Ordinary);
        Assert.Equal(Ordinary, surface.Presenter);
        Assert.Equal(new WindowRect(100, 100, 1100, 700), surface.Bounds);
    }

    [Fact]
    public async Task CompactMoveOnlyPersistsTheCompactOrigin()
    {
        var (surface, state, ratio, presentation) = PlayerWindowPresentationTestFactory.Create();
        using var _ = ratio;
        state.Compact = new(300, 600);
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;
        await presentation.RequestModeAsync(Compact);
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
        surface.PresenterBounds = mode => mode == Compact
            ? new WindowRect(surface.Bounds.X, surface.Bounds.Y, 640, 360)
            : null;

        await presentation.RequestModeAsync(Compact);
        await presentation.RequestModeAsync(Ordinary);

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