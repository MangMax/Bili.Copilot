#nullable enable
using Richasy.Danmaku;
static partial class Program
{
    static void BoundaryCases(Action<string, Action> run)
    {
        Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(0x00010008);
        try
        {
            foreach (bool pause in new[] { true, false })
            {
                string control = pause ? "Pause" : "Resume";
                run("R1_" + control + "_ReentrantOppositeThrow_NewestWins", () =>
                {
                    using var r = new Rig();
                    var error = new ApplicationException("outer callback");
                    Task<CoreResourceOutcome>? latest = null;
                    Action callback = () => { latest = pause ? r.Bridge.ResumeAsync() : r.Bridge.PauseAsync(); throw error; };
                    if (pause) r.Bridge.Scheduler.PauseRequested += callback;
                    else r.Bridge.Scheduler.ResumeRequested += callback;
                    Exception? seen = null;
                    try { if (pause) r.Bridge.PauseAsync(); else r.Bridge.ResumeAsync(); }
                    catch (Exception e) { seen = e; }
                    Check(ReferenceEquals(seen, error), "exception identity changed");
                    r.Applied(latest!); r.Applied(r.Bridge.FrameAsync());
                    Check(r.Bridge.Scheduler.ReadCurrent().Error == null, "old exception faulted newer control");
                });
                run("R1_" + control + "_ClosedBeforeIdentity", () =>
                {
                    using var r = new Rig();
                    Action callback = () => { if (pause) r.Bridge.Scheduler.Resume(); else r.Bridge.Scheduler.Pause(); r.Bridge.Dispose(); };
                    if (pause) r.Bridge.Scheduler.PauseRequested += callback; else r.Bridge.Scheduler.ResumeRequested += callback;
                    var t = pause ? r.Bridge.PauseAsync() : r.Bridge.ResumeAsync(); Done(t);
                    Check(t.Result == CoreResourceOutcome.Closed, "identity hid close");
                });
                run("R1_" + control + "_FaultAndOriginalException", () =>
                {
                    using var r = new Rig(); var error = new ApplicationException("current callback");
                    Action callback = () => throw error;
                    if (pause) r.Bridge.Scheduler.PauseRequested += callback; else r.Bridge.Scheduler.ResumeRequested += callback;
                    Exception? seen = null;
                    try { if (pause) r.Bridge.PauseAsync(); else r.Bridge.ResumeAsync(); } catch (Exception e) { seen = e; }
                    Check(ReferenceEquals(seen,error) && ReferenceEquals(r.Bridge.Scheduler.ReadCurrent().Error,error), "fault identity lost");
                    var late = pause ? r.Bridge.PauseAsync() : r.Bridge.ResumeAsync(); Done(late);
                    Check(late.Result == CoreResourceOutcome.Faulted, "fault admitted control");
                });
            }
            run("R1_StopCallbackResumes_ReceiptMustBeSuperseded", () =>
            {
                using var r = new Rig(); Task<CoreResourceOutcome>? resumed = null;
                r.Bridge.Scheduler.StopRequested += () => resumed = r.Bridge.ResumeAsync();
                var stopped = r.Bridge.StopAsync(); Done(stopped); r.Applied(resumed!);
                r.Time.Milliseconds = 100; r.Applied(r.Bridge.FrameAsync());
                Check(r.Surface.Steps.Last().Step == TimeSpan.FromMilliseconds(100), "Resume did not take effect");
                Check(stopped.Result == CoreResourceOutcome.Superseded, "obsolete Stop receipt=" + stopped.Result + "; next actual step=" + r.Surface.Steps.Last().Step);
            });
            foreach (bool clear in new[] { false, true })
            {
                run("R1_" + (clear ? "Clear" : "Set") + "CallbackReplacesScene_ReceiptMustBeSuperseded", () =>
                {
                    using var r = new Rig(); int once = 0; long replacementScene = -1;
                    r.Bridge.Scheduler.StopRequested += () =>
                    {
                        if (++once != 1) return;
                        r.Bridge.SetListAsync(new() { Entry("replacement") });
                        replacementScene = r.Bridge.Scheduler.ReadCurrent().Scene;
                    };
                    var old = clear ? r.Bridge.ClearAsync() : r.Bridge.SetListAsync(new() { Entry("outer") });
                    Done(old); r.Applied(r.Bridge.FrameAsync());
                    Check(r.Bridge.Scheduler.ReadCurrent().Scene == replacementScene, "outer scene resurrected");
                    // Keep the documented outer-Set-wins collection alias rule separate from scene receipt identity.
                    Check(r.Bridge.Scheduler.GetDanmakuList().Single().Text == (clear ? "replacement" : "outer"), "collection compatibility changed");
                    Check(old.Result == CoreResourceOutcome.Superseded, "obsolete scene receipt=" + old.Result + "; current scene=" + replacementScene);
                });
            }
            foreach (bool realtime in new[] { false, true })
            {
                run("R1_" + (realtime ? "Realtime" : "Selection") + "_QueuedIntentChange_RetriesOnce", () =>
                {
                    using var r = new Rig(); r.Applied(r.Bridge.SetListAsync(new() { Entry("selected") }));
                    using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
                    var held = r.Owner.ExecuteAsync(r.Owner.Capture(), _ => { entered.Set(); Check(release.Wait(10000), "hold timed out"); });
                    Check(entered.Wait(10000), "hold not entered");
                    Task<CoreResourceOutcome>? selected = null; Task<CoreResourceOutcome> paused;
                    try
                    {
                        if (realtime) r.Bridge.AddRealtime(Entry("realtime"), false);
                        else { selected = r.Bridge.SeekAsync(0); var frames = typeof(CoreResourceOwner).GetField("frames", F)!.GetValue(r.Owner)!; Check(SpinWait.SpinUntil(() => (int)frames.GetType().GetProperty("Count")!.GetValue(frames)! > 0, 10000), "selection frame not queued"); }
                        paused = r.Bridge.PauseAsync();
                    }
                    finally { release.Set(); }
                    Done(held); if (selected != null) r.Applied(selected); r.Applied(paused); r.Applied(r.Bridge.FrameAsync());
                    Check(r.Items().Length == 1 && r.Items()[0].Item.Text == (realtime ? "realtime" : "selected"), "lost or duplicate preparation");
                    Check(r.Surface.Steps.Last().Step == TimeSpan.Zero, "retry ignored latest pause");
                });
            }
        }
        finally { Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown(); }
    }
}
