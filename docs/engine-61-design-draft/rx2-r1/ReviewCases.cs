#nullable enable
// Focused receipt identity counterexamples, using the delivered real native rig.
static partial class Program
{
    static void ReviewCases(Action<string, Action> run)
    {
        Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(0x00010008);
        try
        {
            run("Review_PauseQueuedThenResume_PauseMustBeSuperseded", () =>
            {
                using var r = new Rig();
                r.Applied(r.Bridge.ResumeAsync());
                using var entered = new ManualResetEventSlim();
                using var release = new ManualResetEventSlim();
                var held = r.Owner.ExecuteAsync(r.Owner.Capture(), _ =>
                {
                    entered.Set();
                    Check(release.Wait(10000), "hold timeout");
                });
                Check(entered.Wait(10000), "hold not entered");
                Task<Richasy.Danmaku.CoreResourceOutcome> pause, resume;
                try
                {
                    pause = r.Bridge.PauseAsync();
                    resume = r.Bridge.ResumeAsync();
                }
                finally { release.Set(); }
                Done(held); Done(pause); r.Applied(resume);
                r.Time.Milliseconds = 100;
                r.Applied(r.Bridge.FrameAsync());
                Check(r.Surface.Steps.Last().Step == TimeSpan.FromMilliseconds(100), "new Resume did not execute");
                Check(pause.Result == Richasy.Danmaku.CoreResourceOutcome.Superseded,
                    $"obsolete Pause receipt was {pause.Result}; no paused frame executed before Resume, next step is {r.Surface.Steps.Last().Step}");
            });
            run("Review_PauseCallbackResumes_PauseMustBeSuperseded", () =>
            {
                using var r = new Rig();
                r.Applied(r.Bridge.ResumeAsync());
                Task<Richasy.Danmaku.CoreResourceOutcome>? resume = null;
                r.Bridge.Scheduler.PauseRequested += () => resume = r.Bridge.ResumeAsync();
                var pause = r.Bridge.PauseAsync();
                Done(pause); r.Applied(resume!);
                r.Time.Milliseconds = 100;
                r.Applied(r.Bridge.FrameAsync());
                Check(r.Surface.Steps.Last().Step == TimeSpan.FromMilliseconds(100), "new Resume did not execute");
                Check(pause.Result == Richasy.Danmaku.CoreResourceOutcome.Superseded,
                    $"reentrantly superseded Pause receipt was {pause.Result}; current state is playing");
            });
            run("Review_ResumeQueuedThenPause_ResumeMustBeSuperseded", () =>
            {
                using var r = new Rig();
                r.Applied(r.Bridge.PauseAsync());
                using var entered = new ManualResetEventSlim();
                using var release = new ManualResetEventSlim();
                var held = r.Owner.ExecuteAsync(r.Owner.Capture(), _ =>
                {
                    entered.Set();
                    Check(release.Wait(10000), "hold timeout");
                });
                Check(entered.Wait(10000), "hold not entered");
                Task<Richasy.Danmaku.CoreResourceOutcome> resume, pause;
                try
                {
                    resume = r.Bridge.ResumeAsync();
                    pause = r.Bridge.PauseAsync();
                }
                finally { release.Set(); }
                Done(held); Done(resume); r.Applied(pause);
                r.Time.Milliseconds = 100;
                r.Applied(r.Bridge.FrameAsync());
                Check(r.Surface.Steps.Last().Step == TimeSpan.Zero, "new Pause did not execute");
                Check(resume.Result == Richasy.Danmaku.CoreResourceOutcome.Superseded,
                    $"obsolete Resume receipt was {resume.Result}; no resumed frame executed before Pause, next step is {r.Surface.Steps.Last().Step}");
            });
            run("Review_ResumeCallbackPauses_ResumeMustBeSuperseded", () =>
            {
                using var r = new Rig();
                r.Applied(r.Bridge.PauseAsync());
                Task<Richasy.Danmaku.CoreResourceOutcome>? pause = null;
                r.Bridge.Scheduler.ResumeRequested += () => pause = r.Bridge.PauseAsync();
                var resume = r.Bridge.ResumeAsync();
                Done(resume); r.Applied(pause!);
                r.Time.Milliseconds = 100;
                r.Applied(r.Bridge.FrameAsync());
                Check(r.Surface.Steps.Last().Step == TimeSpan.Zero, "new Pause did not execute");
                Check(resume.Result == Richasy.Danmaku.CoreResourceOutcome.Superseded,
                    $"reentrantly superseded Resume receipt was {resume.Result}; current state is playing");
            });
        }
        finally { Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown(); }
    }
}
