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
        status = new TextBlock { Text = "IDLE" };
        AutomationProperties.SetAutomationId(status, "Status");
        var start = new Button { Content = "Run Legacy seek checks" };
        AutomationProperties.SetAutomationId(start, "Start");
        start.Click += async (_, _) => { start.IsEnabled = false; try { await Suite(); } catch (Exception ex) { Log("FATAL", new { passed = false, error = ex.ToString() }); } finally { status.Text = "DONE"; } };
        var cont = new Button { Content = "Continue" };
        AutomationProperties.SetAutomationId(cont, "Continue");
        cont.Click += (_, _) => next?.TrySetResult();
        var controls = new StackPanel { Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Top };
        controls.Children.Add(status); controls.Children.Add(start); controls.Children.Add(cont); root.Children.Add(controls);
        window = new Window { Title = "Owned Legacy ready 06c7", Content = root };
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
        var asm = typeof(DanmakuFrostMaster).Assembly;
        var type = asm.GetType("Richasy.Danmaku.Legacy.DanmakuRender")!;
        var gate = (ManualResetEventSlim)type.GetField("ProbeGate", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var attempt = (ManualResetEventSlim)type.GetField("ProbeAttempt", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Log("Loaded", new { asm.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(asm.Location))) });
        gate.Reset(); attempt.Reset();
        logger = new ProbeLogger();
        master = new DanmakuFrostMaster(scene, logger: logger);
        master.SetDanmakuEnabledType(true,true,true);
        master.SetDanmakuList(new List<DanmakuItem> { new() { Id="20", Text="TOP20", StartMs=20000, Mode=DanmakuMode.Top, BaseFontSize=40 } });
        master.Pause(); master.Seek(20000); master.UpdateTime(20000);
        await Task.Run(() => {
            if (!attempt.Wait(5000)) throw new TimeoutException("worker submission");
            Thread.Sleep(100);
            Log("BeforeReady", new { device=Read(Render,"_device") is not null, index=Read(master,"_lastIndex"), Time, Paused, rows=SceneState() });
            gate.Set();
        });
        await Until(() => Read(Render,"_device") is not null, "resource ready");
        await Task.Delay(600);
        var state=SceneState();
        Log("AfterReady", new { device=Read(Render,"_device") is not null, index=Read(master,"_lastIndex"), Time, Paused, rows=state });
        Check("NotReadyPausedTargetVisibleAfterReady", Paused && Time==20000 && state.Contains("TOP20") && state.Contains("\"drawable\":true"),state);
        await Visual("READY");
        master.Close(); await Until(() => logger.Exited.IsSet,"worker exit");
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
