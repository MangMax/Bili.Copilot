// Copyright (c) Bili Copilot. All rights reserved.

using System.Diagnostics;

namespace BiliCopilot.UI.Toolkits;

/// <summary>One observed composition frame of the player window.</summary>
internal readonly record struct WindowLayoutSample(
    WindowPresentationMode Mode,
    WindowRect Bounds,
    bool Maximized);

/// <summary>
/// The complete state a transition asks the window manager to settle on: presenter, an
/// optional exact frame, and (for the ordinary presenter) the maximized state.
/// </summary>
internal readonly record struct WindowLayoutTarget(
    WindowPresentationMode Mode,
    WindowRect? Bounds,
    bool Maximized)
{
    /// <summary>The presenter has been applied; only its geometry needs to settle.</summary>
    public static WindowLayoutTarget Presenter(WindowPresentationMode mode)
        => new(mode, null, false);

    /// <summary>The frame must settle on this rectangle.</summary>
    public static WindowLayoutTarget Frame(WindowPresentationMode mode, WindowRect bounds)
        => new(mode, bounds, false);

    /// <summary>The ordinary presenter must be maximized; the system owns the frame.</summary>
    public static WindowLayoutTarget MaximizedState()
        => new(WindowPresentationMode.Ordinary, null, true);
}

/// <summary>How a layout request ended.</summary>
internal enum WindowLayoutOutcome
{
    /// <summary>The requested presenter, maximized state and frame were observed stable.</summary>
    Confirmed,

    /// <summary>
    /// The presenter and maximized state settled, but the window manager kept a stable frame
    /// other than the requested one (minimum size, the maximized work area, a display clamp).
    /// The observed frame is the real one.
    /// </summary>
    Constrained,

    /// <summary>The requested state was not observed stable before the budget elapsed.</summary>
    TimedOut,

    /// <summary>A newer request took the window over, or the window is closing.</summary>
    Cancelled,
}

/// <summary>The settled state of one layout request.</summary>
internal readonly record struct WindowLayoutResult(
    WindowLayoutOutcome Outcome,
    WindowLayoutSample Sample,
    string? Diagnostic = null)
{
    public bool Succeeded
        => Outcome is WindowLayoutOutcome.Confirmed or WindowLayoutOutcome.Constrained;
}

/// <summary>
/// The one completion criterion for every presenter, geometry and maximized request.
/// </summary>
/// <remarks>
/// A composition boundary is a sampling tick, not evidence: the state after each tick is
/// read and only two consecutive identical samples that match the request confirm it. A
/// stable sample that keeps the system's own frame is <see cref="WindowLayoutOutcome.Constrained"/>;
/// a sample that never matches the presenter/maximized request is a timeout, not a success.
/// The budget bounds the whole request, so protection can never become permanent. The
/// window is never read before the tick, after cancellation or after it reports gone.
/// </remarks>
internal static class WindowLayoutSettler
{
    /// <summary>Consecutive identical frames that prove the layout stopped changing.</summary>
    public const int StableFrames = 2;

    public static async Task<WindowLayoutResult> WaitAsync(
        WindowLayoutTarget target,
        TimeSpan budget,
        Func<bool> isGone,
        Func<WindowLayoutSample> read,
        Func<TimeSpan, CancellationToken, Task> tick,
        CancellationToken token)
    {
        var previous = default(WindowLayoutSample);
        var hasPrevious = false;
        var stable = 0;
        var start = Stopwatch.GetTimestamp();
        var limit = (long)Math.Max(0, budget.TotalSeconds * Stopwatch.Frequency);

        while (true)
        {
            if (isGone() || token.IsCancellationRequested)
            {
                return new(WindowLayoutOutcome.Cancelled, previous);
            }

            var remaining = limit - (Stopwatch.GetTimestamp() - start);
            if (remaining <= 0)
            {
                return Conclude(target, previous, hasPrevious, stable);
            }

            try
            {
                var frame = tick(Remaining(remaining), token);
                var finished = await Task.WhenAny(frame, Task.Delay(Remaining(remaining), token));
                if (!ReferenceEquals(finished, frame))
                {
                    // The tick outlived the budget. No newer frame was observed, but the
                    // last stable sample is still the end state to report.
                    return token.IsCancellationRequested
                        ? new(WindowLayoutOutcome.Cancelled, previous)
                        : Conclude(target, previous, hasPrevious, stable);
                }

                await frame;
            }
            catch (OperationCanceledException)
            {
                return new(WindowLayoutOutcome.Cancelled, previous);
            }

            // The tick can outlive the window or be superseded by a newer request; every
            // window read happens only after this second check.
            if (isGone() || token.IsCancellationRequested)
            {
                return new(WindowLayoutOutcome.Cancelled, previous);
            }

            var sample = read();
            if (hasPrevious && sample == previous)
            {
                stable++;
            }
            else
            {
                stable = 0;
                previous = sample;
                hasPrevious = true;
            }

            if (stable >= StableFrames - 1 && Matches(target, sample))
            {
                return new(WindowLayoutOutcome.Confirmed, sample);
            }

            // A stable frame that does not match the request is not success yet: the
            // window manager may still apply the request, and only the end of the budget
            // proves that its own frame is the final one (see Conclude).
        }
    }

    private static bool Matches(WindowLayoutTarget target, WindowLayoutSample sample)
        => sample.Mode == target.Mode
            && (target.Mode != WindowPresentationMode.Ordinary || sample.Maximized == target.Maximized)
            && (target.Bounds is not { } expected || SameFrame(sample.Bounds, expected));

    private static WindowLayoutResult Conclude(WindowLayoutTarget target, WindowLayoutSample last,
        bool hasLast, int stable)
    {
        if (!hasLast)
        {
            return new(WindowLayoutOutcome.TimedOut, last,
                "no player window frame was composed before the budget elapsed");
        }

        var presenterMatches = last.Mode == target.Mode
            && (target.Mode != WindowPresentationMode.Ordinary || last.Maximized == target.Maximized);
        if (stable >= StableFrames - 1 && presenterMatches)
        {
            if (target.Bounds is null || SameFrame(last.Bounds, target.Bounds.Value))
            {
                return new(WindowLayoutOutcome.Confirmed, last);
            }

            return new(WindowLayoutOutcome.Constrained, last,
                FormattableString.Invariant(
                    $"the window manager kept {last.Bounds} instead of the requested {target.Bounds}"));
        }

        return new(WindowLayoutOutcome.TimedOut, last, Unmet(target, last, hasLast));
    }

    /// <summary>Frame equality within the one-pixel tolerance of DPI rounding.</summary>
    public static bool SameFrame(WindowRect left, WindowRect right)
        => Math.Abs(left.X - right.X) <= 1
            && Math.Abs(left.Y - right.Y) <= 1
            && Math.Abs(left.Width - right.Width) <= 1
            && Math.Abs(left.Height - right.Height) <= 1;

    private static TimeSpan Remaining(long ticks)
        => ticks <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);

    private static string Unmet(WindowLayoutTarget target, WindowLayoutSample last, bool hasLast)
    {
        if (!hasLast)
        {
            return "no player window frame was composed before the budget elapsed";
        }

        if (last.Mode != target.Mode)
        {
            return FormattableString.Invariant(
                $"the presenter stayed {last.Mode} instead of becoming {target.Mode}");
        }

        if (target.Mode == WindowPresentationMode.Ordinary && last.Maximized != target.Maximized)
        {
            return FormattableString.Invariant(
                $"the ordinary presenter never reached maximized={target.Maximized}");
        }

        return target.Bounds is { } expected
            ? FormattableString.Invariant(
                $"the frame moved or stayed at {last.Bounds}, never settling at {expected}")
            : "the player window frame did not become stable before the budget elapsed";
    }

}