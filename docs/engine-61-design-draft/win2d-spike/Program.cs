// EXPERIMENT ONLY: bounded blocking probes, never a shipping engine/library.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace SpikeHarness;

static class StartupTrace
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init() => Write("module-init");
    internal static void Write(string value) => File.AppendAllText(Path.Combine(Path.GetTempPath(), "Win2DSpike702c-startup.txt"), Environment.ProcessId + " " + value + Environment.NewLine);
}

public sealed partial class SpikeApp : Application
{
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static readonly ConcurrentQueue<object> Events = new();
    static readonly string Output = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "events.jsonl");
    static int sequence;
    Window window = null!;
    Grid host = null!;
    TextBlock status = null!;
    readonly List<Probe> probes = new(); // Explicit strong references; NOT evidence of a live game loop.
    Microsoft.UI.Dispatching.DispatcherQueueTimer? heartbeat;
    int beats;
    volatile bool finished;
    string current = "startup";
    internal static void Log(string test, string name, object? data = null)
        => Events.Enqueue(new { seq = Interlocked.Increment(ref sequence), ms = Clock.Elapsed.TotalMilliseconds, tid = GetCurrentThreadId(), managed = Environment.CurrentManagedThreadId, test, name, data });

    public SpikeApp()
    {
        StartupTrace.Write("app-constructor");
        InitializeComponent();
        StartupTrace.Write("component-initialized");
        UnhandledException += (_, e) => { Log(current, "UnhandledException", new { e.Message, hr = e.Exception.HResult }); e.Handled = true; };
        var writer = new Thread(() => {
            using var file = new StreamWriter(Output, false) { AutoFlush = true };
            while (!finished || !Events.IsEmpty) { while (Events.TryDequeue(out var row)) file.WriteLine(JsonSerializer.Serialize(row)); Thread.Sleep(10); }
        }) { IsBackground = true, Name = "Experiment evidence writer" };
        writer.Start();
        new Thread(() => { Thread.Sleep(90000); if (!finished) { Log("watchdog", "HardProcessTimeout"); Thread.Sleep(100); Environment.Exit(90); } }) { IsBackground = true }.Start();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupTrace.Write("launched");
        window = new() { Title = "EXPERIMENT Win2D 1.3.2 retirement 702c" };
        host = new() { Height = 260 };
        status = new() { Text = "READY - isolated Win2D capability experiment", FontSize = 20 };
        var run = new Button { Content = "Run bounded suite" };
        AutomationProperties.SetAutomationId(run, "RunSuite");
        AutomationProperties.SetAutomationId(status, "Status");
        run.Click += async (_, _) => { run.IsEnabled = false; await RunSuite(); };
        window.Content = new StackPanel { Padding = new Thickness(24), Spacing = 16, Children = { status, run, host } };
        window.Activate();
        StartupTrace.Write("window-activated");
        heartbeat = window.DispatcherQueue.CreateTimer();
        heartbeat.Interval = TimeSpan.FromMilliseconds(50);
        heartbeat.Tick += (_, _) => { beats++; Log(current, "Heartbeat", beats); };
        heartbeat.Start();
        Log("startup", "Identity", new { pid = Environment.ProcessId, package = Windows.ApplicationModel.Package.Current.Id.FullName, frequency = Stopwatch.Frequency, releaseEvent = "Local\\Win2DSpike702c-" + Environment.ProcessId });
        foreach (var module in Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Where(m => m.ModuleName.Contains("Canvas") || m.ModuleName == "Microsoft.UI.Xaml.dll"))
            Log("startup", "LoadedModule", new { module.ModuleName, module.FileName, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.FileName))) });
    }

    async Task RunSuite()
    {
        try
        {
            foreach (var strategy in new[] { "queue", "stopped" })
                foreach (var kind in new[] { "normal", "parent-first", "never-loaded", "pending-resources", "blocked-close-clear", "blocked-remove", "blocked-framework-clear" })
                {
                    current = strategy + "/" + kind;
                    status.Text = current;
                    Log(current, "Begin", new { beats });
                    var p = new Probe(current, strategy == "stopped", kind, window.DispatcherQueue);
                    probes.Add(p);
                    if (kind != "never-loaded") host.Children.Add(p.Canvas);
                    if (kind == "pending-resources" || kind == "resource-failure") await p.ResourcesEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    else if (kind != "never-loaded") await p.FirstDraw.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    if (current == "queue/normal") {
                        foreach (var module in Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Where(m => m.ModuleName.Contains("Canvas") || m.ModuleName == "Microsoft.UI.Xaml.dll"))
                            Log(current, "LoadedModule", new { module.ModuleName, module.FileName, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.FileName))) });
                        Log(current, "VisualReady"); status.Text = "queue/normal visual-ready"; await Task.Delay(4000);
                    }
                    await Task.Delay(150);
                    if (kind.StartsWith("blocked"))
                    {
                        p.StartBarrier();
                        await p.BarrierEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        Log(current, "BarrierUiObservationStart", beats);
                        await Task.Delay(250); // Observation window, not a completion fence.
                        Log(current, "BarrierUiObservationEnd", beats);
                    }
                    if (kind == "normal")
                    {
                        p.Close();
                        if (p.CleanupTask != null) await p.CleanupTask.WaitAsync(TimeSpan.FromSeconds(3));
                        p.Remove();
                        Clear();
                    }
                    else if (kind == "parent-first" || kind == "blocked-framework-clear") { Clear(); await Task.Delay(150); p.Close(); p.Remove(); }
                    else if (kind == "blocked-remove") { p.Close(); p.Remove(); Clear(); }
                    else { p.Close(); Clear(); await Task.Delay(150); p.Remove(); }
                    p.CompleteResources();
                    await Task.Delay(350); // Bounded observation after actual unload/stop; absence remains absence.
                    _ = p.Queue("post-remove-probe", false);
                    await Task.Delay(150);
                    p.RecordSummary();
                    // Harness-only rescue, after a stopped ack or never-loaded. Not candidate cleanup success.
                    p.Rescue();
                    Log(current, "End", new { beats });
                }
            status.Text = "DONE - evidence saved; see per-case retirement results";
            Log("suite", "Complete", new { count = probes.Count });
        }
        catch (Exception ex) { status.Text = "FAILED: " + ex.GetType().Name; Log(current, "SuiteFailure", new { ex.Message, ex.StackTrace }); }
        finally { heartbeat?.Stop(); finished = true; }
    }

    void Clear()
    {
        Log(current, "ChildrenClearEnter", beats);
        host.Children.Clear();
        Log(current, "ChildrenClearReturn", beats);
    }

    sealed class Probe
    {
        public readonly CanvasAnimatedControl Canvas = new();
        public readonly TaskCompletionSource FirstDraw = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource ResourcesEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource BarrierEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource resources = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly string id, kind;
        readonly bool stopCleanup;
        readonly Microsoft.UI.Dispatching.DispatcherQueue ui;
        readonly List<(string Name, IAsyncAction Action)> actions = new();
        CanvasRenderTarget? target;
        int creates, uses, disposals, closed, stopped, loaded, unloaded;
        uint owner;
        public Task? CleanupTask;
        public Probe(string id, bool stopCleanup, string kind, Microsoft.UI.Dispatching.DispatcherQueue ui)
        {
            this.id = id; this.stopCleanup = stopCleanup; this.kind = kind; this.ui = ui;
            Canvas.Loaded += (_, _) => { loaded++; Log(id, "Loaded"); };
            Canvas.Unloaded += (_, _) => { unloaded++; Log(id, "Unloaded"); };
            Canvas.CreateResources += (_, e) => {
                Log(id, "CreateResources", new { reason = e.Reason.ToString() });
                if (kind == "pending-resources" || kind == "resource-failure") e.TrackAsyncAction(resources.Task.AsAsyncAction());
                ResourcesEntered.TrySetResult();
                if (kind == "resource-failure") new Thread(() => { Thread.Sleep(80); Log(id, "InjectResourceFailure"); resources.TrySetException(new InvalidOperationException("EXPERIMENT synthetic CreateResources failure")); }) { IsBackground = true }.Start();
            };
            Canvas.GameLoopStarting += (_, _) => { owner = GetCurrentThreadId(); Log(id, "GameLoopStarting"); };
            // Pre-subscribed before insertion. Handler NEVER waits for or dispatches to UI.
            Canvas.GameLoopStopped += (_, _) => { stopped++; Log(id, "GameLoopStoppedEnter"); if (stopCleanup) Release("GameLoopStopped"); Log(id, "GameLoopStoppedReturn"); };
            Canvas.Update += (_, _) => { if (uses == 0) Log(id, "Update"); };
            Canvas.Draw += (_, e) => {
                if (Volatile.Read(ref closed) != 0) return;
                if (target == null) { target = new CanvasRenderTarget(Canvas.Device, 64, 64, 96); creates++; using (var ds = target.CreateDrawingSession()) ds.Clear(Windows.UI.Color.FromArgb(255, 30, 130, 210)); Log(id, "ResourceCreated", new { creates }); }
                e.DrawingSession.Clear(Windows.UI.Color.FromArgb(255, 20, 25, 35));
                e.DrawingSession.DrawImage(target, 20, 20);
                e.DrawingSession.DrawText("REAL CanvasAnimatedControl 1.3.2", 100, 30, Windows.UI.Color.FromArgb(255, 255, 255, 255));
                uses++;
                Log(id, "ResourceLastUse", new { uses });
                FirstDraw.TrySetResult();
            };
        }
        public void Close()
        {
            Log(id, "CloseEnter");
            Interlocked.Exchange(ref closed, 1);
            if (!stopCleanup) CleanupTask = Queue("cleanup", true);
            Log(id, "CloseReturn");
        }
        public Task Queue(string name, bool release)
        {
            Log(id, "QueueEnter", name);
            try {
                var action = Canvas.RunOnGameLoopThreadAsync(() => { Log(id, "QueueCallback", name); if (release) Release("queue"); });
                actions.Add((name, action));
                Log(id, "QueueReturn", new { name, status = action.Status.ToString() });
                return Observe(action, name);
            } catch (Exception ex) { Log(id, "QueueThrow", new { name, ex.Message, hr = ex.HResult }); return Task.CompletedTask; }
        }
        async Task Observe(IAsyncAction action, string name)
        {
            try { await action.AsTask().ConfigureAwait(false); Log(id, "ActionCompleted", name); }
            catch (Exception ex) { Log(id, "ActionFailed", new { name, type = ex.GetType().Name, hr = ex.HResult, status = action.Status.ToString() }); }
        }
        public void Remove() { Log(id, "RemoveEnter"); Canvas.RemoveFromVisualTree(); Log(id, "RemoveReturn"); }
        public void CompleteResources() { Log(id, "CompleteResourcesRequest"); resources.TrySetResult(); }
        void Release(string via)
        {
            Log(id, "ReleaseEnter", new { via, hasTarget = target != null, owner });
            if (target != null) { target.Dispose(); target = null; disposals++; Log(id, "ResourceDisposed", new { via, disposals }); }
            Log(id, "ReleaseReturn", via);
        }
        public void StartBarrier()
        {
            var gate = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\Win2DSpike702c-" + Environment.ProcessId);
            gate.Reset();
            var action = Canvas.RunOnGameLoopThreadAsync(() => {
                Log(id, "BarrierEnter"); BarrierEntered.TrySetResult();
                new Thread(() => { Thread.Sleep(1500); Log(id, "ExternalThreadRelease"); gate.Set(); }) { IsBackground = true, Name = "Experiment barrier release independent of UI" }.Start();
                bool signaled = gate.WaitOne(2500);
                Log(id, "BarrierReturn", new { signaled });
            });
            actions.Add(("barrier", action));
            _ = Observe(action, "barrier");
        }
        public void RecordSummary() => Log(id, "Summary", new { creates, uses, disposals, owner, loaded, unloaded, stopped, targetAlive = target != null, actions = actions.Select(a => new { a.Name, status = a.Action.Status.ToString() }).ToArray() });
        public void Rescue() { if (target != null && (stopped > 0 || loaded == 0)) Release("HARNESS-RESCUE-UI-NOT-CANDIDATE"); }
    }
}
