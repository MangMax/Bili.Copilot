#nullable enable
using Richasy.Danmaku;
static partial class Program
{
    static void RevisionCases(Action<string, Action> run)
    {
        Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(0x00010008);
        try
        {
            foreach (var kind in new[] { "Configuration", "Debug", "Layer" })
            run("IX_" + kind + "ChangesBetweenIdentityCheckAndSnapshot", () =>
            {
                var owner = new CoreResourceOwner(new(), new Binding(), () => new Surface());
                using var bridge = new CoreExecutionBridge(owner);
                using var entered = new ManualResetEventSlim();
                using var release = new ManualResetEventSlim();
                int once = 0;
                bridge.ReviewConfigurationChecked = revision =>
                {
                    if (revision.HasValue && Interlocked.Increment(ref once) == 1)
                    {
                        entered.Set();
                        Check(release.Wait(10000), "configuration checkpoint timeout");
                    }
                };
                // Same caller thread publishes both configurations; the old one resumes
                // asynchronously from Ready, exactly as an initially zero-size facade does.
                Task<CoreResourceOutcome> Request(bool next) => kind switch
                {
                    "Debug" => bridge.SetDebugAsync(next),
                    "Layer" => bridge.SetLayerAsync(0, next),
                    _ => bridge.ConfigureAsync(new() { FontSizeScale = next ? 2 : 1 }),
                };
                var old = Request(false);
                Check(!old.IsCompleted, "old request must initially await Ready");
                owner.Resize(640, 360, 1);
                Task<CoreResourceOutcome>? latest = null;
                try
                {
                    Check(entered.Wait(10000), "old request did not reach checkpoint");
                    latest = Request(true);
                    Done(latest); Check(latest.Result == CoreResourceOutcome.Applied, "latest not applied");
                }
                finally { release.Set(); }
                Done(old);
                try { Check(old.Result == CoreResourceOutcome.Superseded,
                    "old configuration borrowed newer successful snapshot: old=" + old.Result + ", latest=" + latest!.Result); }
                finally { bridge.Dispose(); Done(owner.Retirement); Done(bridge.SchedulerCompletion); }
            });
        }
        finally { Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown(); }
    }
}
