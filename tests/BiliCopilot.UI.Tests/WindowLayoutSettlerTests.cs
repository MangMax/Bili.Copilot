using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class WindowLayoutSettlerTests
{
    private const WindowPresentationMode Compact = WindowPresentationMode.CompactOverlay;
    private const WindowPresentationMode Ordinary = WindowPresentationMode.Ordinary;
    private static readonly WindowRect Target = new(300, 600, 640, 360);

    [Fact]
    public async Task AStableFrameThatDiffersFromTheRequestedFrameIsAConstraintNotAConfirmation()
    {
        var constrained = new WindowRect(300, 600, 500, 500);
        var reads = 0;

        var result = await WaitAsync(
            WindowLayoutTarget.Frame(Compact, Target),
            () => { reads++; return new WindowLayoutSample(Compact, constrained, false); },
            TimeSpan.FromMilliseconds(120),
            (_, token) => Task.Delay(1, token));

        Assert.Equal(WindowLayoutOutcome.Constrained, result.Outcome);
        Assert.Equal(constrained, result.Sample.Bounds);
        Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
        Assert.True(reads > 2);
    }

    [Fact]
    public async Task TwoConsecutiveMatchingFramesAreRequiredBeforeTheRequestIsConfirmed()
    {
        var frames = new Queue<WindowLayoutSample>([
            new(Compact, new WindowRect(300, 600, 700, 400), false),
            new(Compact, new WindowRect(300, 600, 700, 400), false),
            new(Compact, Target, false),
            new(Compact, Target, false),
        ]);
        var reads = 0;

        var result = await WaitAsync(WindowLayoutTarget.Frame(Compact, Target), () =>
        {
            reads++;
            return frames.Dequeue();
        });

        Assert.Equal(WindowLayoutOutcome.Confirmed, result.Outcome);
        Assert.Equal(Target, result.Sample.Bounds);
        Assert.Equal(4, reads);
    }

    [Fact]
    public async Task FramesThatKeepChangingReachTheDeadlineAsATimeout()
    {
        var reads = 0;

        var result = await WaitAsync(
            WindowLayoutTarget.Frame(Compact, Target),
            () => new WindowLayoutSample(Compact, new WindowRect(0, 0, 600 + reads++, 360), false),
            TimeSpan.FromMilliseconds(120),
            (_, token) => Task.Delay(1, token));

        Assert.Equal(WindowLayoutOutcome.TimedOut, result.Outcome);
        Assert.Contains("settl", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.True(reads > 2);
    }

    [Fact]
    public async Task AStableFrameThatNeverMatchesTheRequestedPresenterIsATimeoutNotAConstraint()
    {
        var result = await WaitAsync(
            WindowLayoutTarget.Presenter(Ordinary),
            () => new WindowLayoutSample(Compact, Target, false),
            TimeSpan.FromMilliseconds(120),
            (_, token) => Task.Delay(1, token));

        Assert.Equal(WindowLayoutOutcome.TimedOut, result.Outcome);
        Assert.Equal(Compact, result.Sample.Mode);
        Assert.Contains("presenter", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheMaximizedRequirementAppliesOnlyToTheOrdinaryPresenter()
    {
        var compact = await WaitAsync(
            WindowLayoutTarget.Presenter(Compact),
            () => new WindowLayoutSample(Compact, Target, true));

        Assert.Equal(WindowLayoutOutcome.Confirmed, compact.Outcome);

        var missingMaximize = await WaitAsync(
            WindowLayoutTarget.MaximizedState(),
            () => new WindowLayoutSample(Ordinary, Target, false),
            TimeSpan.FromMilliseconds(120),
            (_, token) => Task.Delay(1, token));

        Assert.Equal(WindowLayoutOutcome.TimedOut, missingMaximize.Outcome);

        var maximized = await WaitAsync(
            WindowLayoutTarget.MaximizedState(),
            () => new WindowLayoutSample(Ordinary, new WindowRect(-11, -11, 3862, 2110), true));

        Assert.Equal(WindowLayoutOutcome.Confirmed, maximized.Outcome);
    }

    [Fact]
    public async Task CancellationBeforeTheFirstFrameNeverReadsTheWindow()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var reads = 0;

        var result = await WaitAsync(
            WindowLayoutTarget.Frame(Compact, Target),
            () => { reads++; return new WindowLayoutSample(Compact, Target, false); },
            token: cancellation.Token);

        Assert.Equal(WindowLayoutOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task CancellationDuringAFrameNeverReadsTheWindowAfterwards()
    {
        using var cancellation = new CancellationTokenSource();
        var reads = 0;

        var result = await WaitAsync(
            WindowLayoutTarget.Frame(Compact, Target),
            () => { reads++; return new WindowLayoutSample(Compact, Target, false); },
            token: cancellation.Token,
            tick: async (_, _) =>
            {
                await cancellation.CancelAsync();
            });

        Assert.Equal(WindowLayoutOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task ADestroyedWindowIsNeverReadAfterAFrame()
    {
        var gone = false;
        var reads = 0;

        var result = await WaitAsync(
            WindowLayoutTarget.Frame(Compact, Target),
            () => { reads++; return new WindowLayoutSample(Compact, Target, false); },
            tick: (_, _) => { gone = true; return Task.CompletedTask; },
            gone: () => gone);

        Assert.Equal(WindowLayoutOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task ACompositionBoundaryThatNeverArrivesStillEndsAtTheDeadline()
    {
        var never = new TaskCompletionSource();
        var started = Environment.TickCount64;

        var result = await WaitAsync(
            WindowLayoutTarget.Frame(Compact, Target),
            () => new WindowLayoutSample(Compact, Target, false),
            TimeSpan.FromMilliseconds(120),
            (_, _) => never.Task);

        Assert.Equal(WindowLayoutOutcome.TimedOut, result.Outcome);
        Assert.True(Environment.TickCount64 - started < 2000);
    }

    private static Task<WindowLayoutResult> WaitAsync(
        WindowLayoutTarget target,
        Func<WindowLayoutSample> read,
        TimeSpan? budget = null,
        Func<TimeSpan, CancellationToken, Task>? tick = null,
        CancellationToken token = default,
        Func<bool>? gone = null)
        => WindowLayoutSettler.WaitAsync(
            target,
            budget ?? TimeSpan.FromMilliseconds(200),
            gone ?? (() => false),
            read,
            tick ?? ((_, _) => Task.CompletedTask),
            token);
}
