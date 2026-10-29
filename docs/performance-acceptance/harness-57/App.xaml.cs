using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Richasy.Danmaku;
using Richasy.Danmaku.Legacy;
using Richasy.Danmaku.Models;
using Richasy.Danmaku.Enums;

namespace Spike57;

// Offline experiment only: actual package renderers, synthetic clock and segment source.
public partial class App : Application
{
    Window window;
    Grid surface;
    TextBlock status;
    DanmakuRenderer dx;
    DanmakuFrostMaster legacy;
    string engine = "none";
    int requests;
    bool busy;
    bool closed;
    readonly CancellationTokenSource lifetime = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly string log = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "measurements.jsonl");
    public App() { InitializeComponent(); }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new Window { Title = "Issue 57 offline engines — owned A35E" };
        var root = new Grid { Padding = new Thickness(16), RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        var controls = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };
        Add(controls, "DirectX", () => Select(true));
        Add(controls, "Win2D", () => Select(false));
        Add(controls, "Window", () => Load(false, false));
        Add(controls, "Full", () => Load(true, false));
        Add(controls, "Dense", () => Load(false, true));
        Add(controls, "Append", Append);
        Add(controls, "Replace", Replace);
        Add(controls, "Seek", Seek);
        Add(controls, "Pause", () => { Pause(); Sample("pause"); return Task.CompletedTask; });
        Add(controls, "Resume", () => { dx?.Resume(); legacy?.Resume(); Sample("resume"); return Task.CompletedTask; });
        Add(controls, "Close", () => { window.Close(); return Task.CompletedTask; });
        Add(controls, "Primary", Primary);
        Add(controls, "Rolling", Rolling);
        Add(controls, "SeekPaused", SeekPaused);
        root.Children.Add(controls);
        status = new TextBlock { Text = "Select an engine", TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(status, "Status");
        Grid.SetRow(status, 1); root.Children.Add(status);
        surface = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Black) };
        Grid.SetRow(surface, 2); root.Children.Add(surface);
        window.Content = root;
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1500, 850));
        window.Closed += (_, _) => { closed = true; lifetime.Cancel(); Release(); Sample("closed"); };
        window.Activate();
        Sample("launched");
    }

    void Add(StackPanel panel, string name, Func<Task> action)
    {
        var b = new Button { Content = name };
        AutomationProperties.SetAutomationId(b, name);
        b.Click += async (_, _) =>
        {
            if (busy && name != "Close") return;
            busy = true;
            try { await action(); }
            catch (OperationCanceledException) { Sample("canceled"); }
            catch (Exception e) { Sample("ERROR: " + e); }
            finally { busy = false; }
        };
        if (panel.Children.Count == 0 || ((StackPanel)panel.Children.Last()).Children.Count == 7)
            panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 });
        ((StackPanel)panel.Children.Last()).Children.Add(b);
    }
    void Release() { dx?.Dispose(); legacy?.Close(); dx = null; legacy = null; surface?.Children.Clear(); }
    Task Select(bool direct)
    {
        Release(); engine = direct ? "DirectX" : "Win2D";
        if (direct) dx = new DanmakuRenderer(surface);
        else { legacy = new DanmakuFrostMaster(surface); legacy.SetDanmakuEnabledType(true, true, true); legacy.SetRollingDensity(-1); }
        Sample("selected"); return Task.CompletedTask;
    }
    static DanmakuItem Item(string id, uint at, string text) => new()
    {
        Id = id, StartMs = at, Text = text, Mode = DanmakuMode.Top, BaseFontSize = 32,
        TextColor = Microsoft.UI.Colors.White, HasOutline = true, FontFamilyName = "Segoe UI"
    };
    void Set(List<DanmakuItem> items) { dx?.SetDanmakuList(items); legacy?.SetDanmakuList(items); }
    void Time(uint t) { dx?.UpdateTime(t); legacy?.UpdateTime(t); }
    void Pause() { dx?.Pause(); legacy?.Pause(); }
    List<DanmakuItem> Items() => dx?.GetDanmakuList() ?? legacy?.GetDanmakuList() ?? [];
    async Task Load(bool full, bool dense)
    {
        if (engine == "none") throw new InvalidOperationException("Select engine first");
        await Select(engine == "DirectX"); // fresh cursor and renderer per measurement
        requests = 0;
        var timer = Stopwatch.StartNew();
        Sample("load-start");
        using var permits = new SemaphoreSlim(6);
        async Task<List<DanmakuItem>> Segment(int n)
        {
            await permits.WaitAsync(lifetime.Token);
            try
            {
                requests++;
                await Task.Delay(n == 120 ? 3000 : 30, lifetime.Token);
                int count = dense ? 20000 : 2;
                return Enumerable.Range(0, count).Select(i => Item($"{n}:{i}", (uint)((n - 1) * 360000 + 1000 + i), n == 1 ? $"FIRST-{i}" : $"S{n}-{i}")).ToList();
            }
            finally { permits.Release(); }
        }
        var result = await Task.WhenAll(Enumerable.Range(1, full ? 120 : 2).Select(Segment));
        var sorted = result.SelectMany(x => x).OrderBy(x => x.StartMs).ToList();
        Set(sorted);
        dx?.Seek(0); legacy?.Seek(0);
        Time(1001);
        Sample($"{(full ? "full" : dense ? "dense" : "window")}-submitted", timer.Elapsed.TotalMilliseconds);
        // Observation delay is explicit, never reported as time-to-first-visible.
        await Task.Delay(250, lifetime.Token); Pause();
        Sample("ready-capture");
    }
    Task Append()
    {
        var input = new List<DanmakuItem> { Item("1:0", 1000, "DUPLICATE"), Item("new", 500, "EARLY-APPEND"), Item("future", 360001, "NEXT") };
        dx?.AddDanmakuList(input); legacy?.AddDanmakuList(input);
        Sample("append", ids: Items().Select(x => $"{x.Id}@{x.StartMs}").Take(10).ToArray());
        return Task.CompletedTask;
    }
    async Task Replace()
    {
        Set([Item("replacement", 1002, "REPLACEMENT")]);
        Time(1003); dx?.Resume(); legacy?.Resume();
        await Task.Delay(250, lifetime.Token); Pause();
        Sample("replace-no-seek");
    }
    async Task Seek()
    {
        // Both engines resume internally. Demonstrate explicit pause restoration after update.
        dx?.Seek(999); legacy?.Seek(999); Time(1003);
        await Task.Delay(250, lifetime.Token); Pause();
        Sample("seek-pause-restored");
    }
    async Task Primary()
    {
        await Select(engine == "DirectX");
        requests = 2;
        var timer = Stopwatch.StartNew();
        Sample("primary-start");
        // Neighbor is deliberately slow. It is only cached in this probe, never appended.
        var neighbor = Task.Delay(3000, lifetime.Token);
        await Task.Delay(30, lifetime.Token);
        Set([Item("primary", 1000, "PRIMARY-BEFORE-NEIGHBOR")]);
        dx?.Seek(0); legacy?.Seek(0); Time(1001);
        Sample("primary-submitted", timer.Elapsed.TotalMilliseconds);
        await Task.Delay(250, lifetime.Token); Pause();
        Sample("primary-visible-probe");
        await neighbor;
        Sample("neighbor-completed-cached-only");
    }
    async Task Rolling()
    {
        await Select(engine == "DirectX");
        // Let the new native surface attach; this is observation setup, not a performance metric.
        await Task.Delay(500, lifetime.Token);
        var item = Item("rolling", 1000, "ROLLING-PAUSE-PROBE"); item.Mode = DanmakuMode.Rolling;
        Set([item]); dx?.Seek(0); legacy?.Seek(0); Time(1001);
        await Task.Delay(800, lifetime.Token); Pause();
        Sample("rolling-paused");
    }
    async Task SeekPaused()
    {
        dx?.Seek(999); legacy?.Seek(999); Time(1003);
        // Deliberately DO NOT restore Pause: observe the package's native Seek contract.
        await Task.Delay(800, lifetime.Token);
        Sample("seek-without-pause-restore");
    }
    void Sample(string phase, double? submitMs = null, string[] ids = null)
    {
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var row = new { utc = DateTimeOffset.UtcNow, elapsedMs = clock.Elapsed.TotalMilliseconds, engine, phase, submitMs, requests,
            count = closed ? 0 : Items().Count, privateBytes = process.PrivateMemorySize64, workingSetBytes = process.WorkingSet64, ids };
        File.AppendAllText(log, JsonSerializer.Serialize(row) + Environment.NewLine);
        if (!closed) status.Text = $"{engine} | {phase} | requests={requests} | count={row.count} | submitMs={submitMs:F1}";
    }
}
