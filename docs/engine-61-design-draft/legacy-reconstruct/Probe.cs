using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Richasy.Danmaku.Legacy;
using Richasy.Danmaku.Models;
using Richasy.Danmaku.Enums;

namespace SpikeHarness;

public sealed partial class SpikeApp : Application
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object? Read(object o, string name) => o.GetType().GetField(name, Flags)!.GetValue(o);
    static string LogPath => Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "events.jsonl");
    static readonly object LogGate = new();
    internal static void Log(string name, object? data = null)
    {
        lock (LogGate) File.AppendAllText(LogPath, JsonSerializer.Serialize(new { utc = DateTime.UtcNow, name, data }) + "\n");
    }
    Window window = null!;
    Grid scene = null!;
    TextBlock status = null!;
    DanmakuFrostMaster? master;
    ProbeLogger logger = null!;
    TaskCompletionSource? next;
    object Render => Read(master!, "_render")!;
    bool Paused => (bool)Read(Render, "_isPaused")!;
    bool Stopped => (bool)Read(Render, "_isStopped")!;
    uint Time => (uint)Read(master!, "_lastTimeMs")!;
    string SceneState()
    {
        var states = new List<object>();
        foreach (var layer in (Array)Read(Render, "_renderLayerList")!)
        {
            var list = (System.Collections.IList)Read(layer!, "RenderList")!;
            lock (list)
                foreach (var item in list)
                    states.Add(new { text = Read(item!, "Text"), drawable = Read(item!, "IsFirstRenderTimeSet"), x = Read(item!, "X"), y = Read(item!, "StartY") });
        }
        return JsonSerializer.Serialize(states);
    }
    public SpikeApp() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var root = new Grid();
        scene = new Grid { Width = 800, Height = 360, Margin = new Thickness(0, 160, 0, 0), VerticalAlignment = VerticalAlignment.Top, Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black) };
        root.Children.Add(scene);
        status = new TextBlock { Text = "READY" };
        AutomationProperties.SetAutomationId(status, "Status");
        var start = new Button { Content = "Run Legacy seek checks" };
        AutomationProperties.SetAutomationId(start, "Start");
        start.Click += async (_, _) => { start.IsEnabled = false; try { await Suite(); } catch (Exception ex) { Log("FATAL", new { passed = false, error = ex.ToString() }); } finally { status.Text = "DONE"; } };
        var cont = new Button { Content = "Continue" };
        AutomationProperties.SetAutomationId(cont, "Continue");
        cont.Click += (_, _) => next?.TrySetResult();
        var controls = new StackPanel { Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Top };
        controls.Children.Add(status); controls.Children.Add(start); controls.Children.Add(cont); root.Children.Add(controls);
        window = new Window { Title = "Owned Legacy seek 1085f94e", Content = root };
        window.Closed += (_, _) => { logger?.Release.Set(); master?.Close(); Log("WindowClosed"); };
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1300, 900)); window.Activate();
    }
    void Check(string name, bool passed, object? evidence = null) => Log(name, new { passed, evidence });
    async Task Until(Func<bool> condition, string reason)
    {
        var clock = Stopwatch.StartNew();
        while (!condition()) { if (clock.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException(reason); await Task.Delay(15); }
    }
    async Task Fresh()
    {
        if (master is not null) { master.Close(); await Until(() => logger.Exited.IsSet, "worker exit"); }
        scene.Children.Clear(); logger = new ProbeLogger();
        master = new DanmakuFrostMaster(scene, logger: logger);
        await Until(() => Read(Render, "_device") is not null, "real canvas resource creation");
        master.SetDanmakuEnabledType(true, true, true);
        master.SetDanmakuList(new List<DanmakuItem> {
            new() { Id = "at20", Text = "PAUSED SEEK 20", StartMs = 20000, Mode = DanmakuMode.Top, BaseFontSize = 32 },
            new() { Id = "at21", Text = "PAUSED SEEK 21", StartMs = 21000, Mode = DanmakuMode.Top, BaseFontSize = 32 },
            new() { Id = "at22", Text = "PAUSED SEEK 22", StartMs = 22000, Mode = DanmakuMode.Top, BaseFontSize = 32 }
        });
        master.Resume(); master.UpdateTime(21000); await Until(() => Time == 21000, "initial time");
        await Task.Delay(150);
    }
    async Task Visual(string phase)
    {
        next = new(TaskCreationOptions.RunContinuationsAsynchronously); status.Text = phase; await next.Task; next = null;
    }
    List<object> Rows()
    {
        var rows = new List<object>();
        foreach (var layer in (Array)Read(Render, "_renderLayerList")!)
        {
            var list = (System.Collections.IList)Read(layer!, "RenderList")!;
            lock (list) foreach (var item in list) rows.Add(item!);
        }
        return rows;
    }
    async Task Select(DanmakuMode mode, uint start = 19000, uint target = 20000, string text = "TARGET", ulong duration = 3000)
    {
        master!.Pause();
        master.SetDanmakuList(new List<DanmakuItem> {
            new() { Id = "old", Text = text, StartMs = start, Mode = mode, BaseFontSize = 32, DurationMs = duration },
            new() { Id = "future", Text = "FUTURE", StartMs = target + 1, Mode = mode, BaseFontSize = 32 }
        });
        master.Seek(target); master.UpdateTime(target); await Task.Delay(300);
    }
    async Task Suite()
    {
        var assembly = typeof(DanmakuFrostMaster).Assembly;
        Log("Loaded", new { assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))), mvid = assembly.ManifestModule.ModuleVersionId });
        await Fresh();
        await Select(DanmakuMode.Top);
        Check("PreTargetTopVisible", Rows().Count == 1 && SceneState().Contains("TARGET"), SceneState());
        Check("CallerStartMsUnchangedAndFutureExcluded", master!.GetDanmakuList()[0].StartMs == 19000 && !SceneState().Contains("FUTURE"), SceneState());
        await Visual("TOP");
        var before = SceneState(); await Task.Delay(3300);
        Check("PausedAgeAndFrameStable", Paused && before == SceneState() && Rows().Count == 1, SceneState());
        await Visual("TOPSTABLE");
        master.Resume(); await Task.Delay(1200);
        Check("ResumeRetainsRemainingLifetime", Rows().Count == 1, SceneState());
        await Task.Delay(1100);
        Check("ResumeExpiresWithoutAgeReset", Rows().Count == 0, SceneState());
        await Select(DanmakuMode.Bottom); Check("PreTargetBottomVisible", Rows().Count == 1, SceneState()); await Visual("BOTTOM");
        foreach (var mode in new[] { DanmakuMode.Rolling, DanmakuMode.ReverseRolling })
        {
            master.SetRollingSpeed(5);
            await Select(mode, 16000);
            var rows = Rows();
            float width = rows.Count == 1 ? (float)Read(rows[0], "Width")! : 0;
            float x = rows.Count == 1 ? (float)Read(rows[0], "X")! : 0;
            float canvas = (float)Render.GetType().GetProperty("CanvasWidth")!.GetValue(Render)!;
            float speed = (float)Read(Render, "_rollingSpeed")!;
            float travel = 4000 * speed * (1 + Math.Min(width * .0015f, .2f));
            float expected = mode == DanmakuMode.Rolling ? canvas - travel : -width + travel;
            Check(mode + "RestoresPosition", rows.Count == 1 && Math.Abs(x-expected)<.05, new { width,x,expected,canvas,speed });
            before = SceneState(); master.Seek(20000); await Task.Delay(200);
            Check(mode + "SameTargetRedraw", before == SceneState() && Rows().Count==1, SceneState());
            await Visual(mode == DanmakuMode.Rolling ? "ROLL" : "REVERSE");
        }
        await Select(DanmakuMode.Top, 10000);
        Check("ExpiredItemsExcluded", Rows().Count == 0, SceneState()); await Visual("BLANK");
        await Select(DanmakuMode.Top,19000,20000,"OTHER"); await Visual("OTHER");
        Check("DifferentTextReplacesOld", SceneState().Contains("OTHER") && !SceneState().Contains("TARGET"), SceneState());
        master.SetDanmakuList(new List<DanmakuItem> { new() { Id="latest", Text="LATEST", StartMs=22000, Mode=DanmakuMode.Top, BaseFontSize=32 } });
        master.UpdateTime(24000); master.UpdateTime(21000); master.UpdateTime(23000); await Task.Delay(500);
        Check("ContinuousSeekLatest", Time==23000 && Paused && SceneState().Contains("LATEST"), new { Time, state=SceneState() });
        await Select(DanmakuMode.Top,19000,20000,"CUSTOM",500);
        Check("CustomDurationExpires", Rows().Count==0, SceneState());
        await Select(DanmakuMode.Top,20000,20000,"AT");
        Check("ExactTargetIncluded", Rows().Count==1, SceneState());
        await Select(DanmakuMode.Top,17000,20000,"BOUNDARY");
        Check("ExactDurationStillVisible", Rows().Count==1, SceneState());
        master.Resume(); await Task.Delay(200);
        Check("BoundaryExpiresOnResume", Rows().Count==0, SceneState());
        master.Close(); await Until(()=>logger.Exited.IsSet,"worker exit"); master.Resume();
        Check("CloseRemainsStopped", Stopped && Paused, new {Stopped,Paused});
        Log("SuiteCompleted");
    }

    sealed class ProbeLogger : ILogger<DanmakuFrostMaster>
    {
        public readonly ManualResetEventSlim Entered = new(), Release = new(), Exited = new();
        int armed;
        public void Arm() { Entered.Reset(); Release.Reset(); Interlocked.Exchange(ref armed, 1); }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception); SpikeApp.Log("EngineLog", text);
            if (text.Contains("Reseek after") && Interlocked.Exchange(ref armed, 0) == 1)
            {
                Entered.Set(); if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("test barrier release");
            }
            if (text.Contains("(): Exited")) Exited.Set();
        }
    }
}
