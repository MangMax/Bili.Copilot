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
        scene = new Grid { Margin = new Thickness(24, 160, 24, 24) };
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
        window.Activate();
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
    async Task Suite()
    {
        var assembly = typeof(DanmakuFrostMaster).Assembly;
        Log("Loaded", new { assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))), mvid = assembly.ManifestModule.ModuleVersionId });
        await Fresh(); master!.Pause(); master.UpdateTime(20000); master.Pause();
        await Until(() => Time == 20000, "backward seek"); await Task.Delay(1000);
        Check("PausedBackward_RemainsPaused", Paused, new { Time, Paused, Stopped });
        var before = SceneState(); await Task.Delay(3500); var after = SceneState();
        Check("PausedBackward_NonemptyStableFrame", before.Contains("\"drawable\":true") && before == after, new { before, after });
        await Visual("BACKWARD");
        master.UpdateTime(21000); master.Pause(); await Until(() => Time == 21000, "short forward"); await Task.Delay(500);
        Check("PausedShortForward_RemainsPaused", Paused, new { Time, Paused, Stopped });
        master.UpdateTime(20000); master.UpdateTime(22000); master.UpdateTime(21000); master.Pause();
        await Task.Delay(1000);
        Check("PausedRepeated_RemainsPaused", Paused && Time == 21000, new { Time, Paused, Stopped });
        var repeated = SceneState(); await Task.Delay(3500);
        Check("PausedRepeated_NonemptyStableFrame", repeated.Contains("\"drawable\":true") && repeated == SceneState(), new { before = repeated, after = SceneState() });
        await Visual("REPEATED");
        master.Resume(); await Task.Delay(500);
        Check("ResumeAfterPausedSeek_Runs", !Paused && !Stopped, new { Time, Paused, Stopped });
        Check("ResumeAfterPausedSeek_PreservesRemainingLifetime", SceneState().Contains("\"drawable\":true"), SceneState());
        await Task.Delay(3200);
        Check("ResumeAfterPausedSeek_ExpiresNormally", SceneState() == "[]", SceneState());
        foreach (var intent in new[] { "Pause", "Resume", "Close" })
        {
            await Fresh();
            logger.Arm(); master!.UpdateTime(20000);
            await Until(() => logger.Entered.IsSet, "queued reseek barrier");
            if (intent == "Pause") master.Pause();
            if (intent == "Resume") { master.Pause(); master.Resume(); }
            if (intent == "Close") master.Close();
            logger.Release.Set();
            if (intent == "Close") await Until(() => logger.Exited.IsSet, "closed worker exit");
            else await Until(() => Time == 20000, "released seek");
            await Task.Delay(250);
            Check("DequeuedSeekThen" + intent, intent == "Close" ? Stopped && Paused : Paused == (intent == "Pause"), new { Time, Paused, Stopped, workerExited = logger.Exited.IsSet });
        }
        master!.Close(); await Until(() => logger.Exited.IsSet, "final worker exit");
        Check("CloseThenResume_RemainsClosed", CloseThenResume(), new { Paused, Stopped });
        Log("SuiteCompleted");
    }
    bool CloseThenResume() { master!.Resume(); return Stopped && Paused; }

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
