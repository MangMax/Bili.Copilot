using System.Reflection;
using System.Text.Json;
using Richasy.Danmaku;
using Richasy.Danmaku.Models;
using Richasy.Danmaku.Legacy;
using BiliCopilot.UI.Models.Constants;

namespace Acceptance49;

public partial class App
{
    sealed class ActualEngine
    {
        readonly DanmakuRenderer dx;
        readonly DanmakuFrostMaster legacy;
        public readonly object Scheduler;
        public readonly object Render;
        public ActualEngine(object dxValue, object legacyValue)
        {
            dx = dxValue as DanmakuRenderer;
            legacy = legacyValue as DanmakuFrostMaster;
            Scheduler = dx != null ? Field(dx, "_scheduler") : legacy;
            Render = Field(dx ?? (object)legacy, "_render");
        }
        public void Time(uint value) { if (dx != null) dx.UpdateTime(value); else legacy.UpdateTime(value); }
        public void Pause() { if (dx != null) dx.Pause(); else legacy.Pause(); }
        public void Resume() { if (dx != null) dx.Resume(); else legacy.Resume(); }
        public void Seek(uint value) { if (dx != null) dx.Seek(value); else legacy.Seek(value); }
        public void Close() { if (dx != null) dx.Dispose(); else legacy.Close(); }
        public void Clear() { if (dx != null) dx.Clear(); else legacy.Clear(); }
        public void List(List<DanmakuItem> value) { if (dx != null) dx.SetDanmakuList(value); else legacy.SetDanmakuList(value); }
        public bool Paused => (bool)Field(Render, "_isPaused");
        public uint LastTime => (uint)Field(Scheduler, "_lastTimeMs");
        public int Index => (int)Field(Scheduler, "_lastIndex");
        public void Hook(string name, Action action) => Scheduler.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Scheduler, action);
    }

    async Task<ActualEngine> ReadyEngine(DanmakuRendererType type)
    {
        Fresh(type);
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (Field(panel, "_danmakuRenderer") == null && Field(panel, "_danmakuMaster") == null)
        {
            if (deadline.ElapsedMilliseconds > 5000) throw new TimeoutException("actual renderer creation");
            await Task.Delay(10);
        }
        await Fence();
        return new ActualEngine(Field(panel, "_danmakuRenderer"), Field(panel, "_danmakuMaster"));
    }

    // Only this test assembly writes instrumented hooks. All behavior goes through public APIs.
    async Task AtDequeued(ActualEngine e, uint time, Action interleave)
    {
        var dequeued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        int entered = 0;
        e.Hook("_testDequeued", () =>
        {
            Interlocked.Exchange(ref entered, 1);
            dequeued.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("test release barrier");
        });
        e.Hook("_testIdle", () => { if (Volatile.Read(ref entered) != 0) done.TrySetResult(); });
        try
        {
            e.Time(time);
            await dequeued.Task.WaitAsync(TimeSpan.FromSeconds(5));
            interleave();
        }
        finally { release.Set(); }
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        e.Hook("_testDequeued", null);
        e.Hook("_testIdle", null);
    }

    async Task RunEngineTests()
    {
        var results = new List<object>();
        var cases = new[] { "DequeuedPause", "DequeuedResume", "DequeuedClear", "DequeuedSetList", "DequeuedSeek", "DequeuedClose", "Zero", "Threshold5000", "Threshold5001", "Backward", "Repeated", "Empty", "End" };
        foreach (var type in new[] { DanmakuRendererType.DirectX, DanmakuRendererType.Win2D })
        foreach (var name in cases)
        {
            try
            {
                var e = await ReadyEngine(type);
                e.List(new uint[] { 0, 500, 1000, 6000, 6001, 90000 }.Select(t => new DanmakuItem
                {
                    Id = "fixture-" + t, StartMs = t, Text = "TARGET " + t,
                    Mode = Richasy.Danmaku.Enums.DanmakuMode.Top, BaseFontSize = 24,
                }).ToList());
                e.Seek(1000);
                e.Resume();
                bool passed;
                switch (name)
                {
                    case "DequeuedPause":
                        await AtDequeued(e, 20000, e.Pause); passed = e.Paused; break;
                    case "DequeuedResume":
                        e.Pause(); await AtDequeued(e, 20000, e.Resume); passed = !e.Paused; break;
                    case "DequeuedClear":
                        await AtDequeued(e, 20000, e.Clear); passed = e.LastTime == 0 && e.Index == 0; break;
                    case "DequeuedSetList":
                        await AtDequeued(e, 20000, () => e.List(new())); passed = e.LastTime == 0 && e.Index == 0; break;
                    case "DequeuedSeek":
                        await AtDequeued(e, 20000, () => e.Seek(2000)); passed = e.LastTime == 2000; break;
                    case "DequeuedClose":
                        uint before = e.LastTime;
                        await AtDequeued(e, 20000, e.Close); passed = e.LastTime == before; break;
                    default:
                        uint target = name switch { "Zero" => 0u, "Threshold5000" => 6000u, "Threshold5001" => 6001u, "Backward" => 500u, "End" => 90000u, _ => 2000u };
                        if (name == "Empty") e.List(new());
                        await AtDequeued(e, target, () => { });
                        int expectedIndex = name switch { "Zero" => 1, "Threshold5000" => 4, "Threshold5001" => 5, "Backward" => 2, "End" => 6, "Empty" => 0, _ => 3 };
                        passed = e.LastTime == target && e.Index == expectedIndex && !e.Paused;
                        if (name == "Repeated")
                        {
                            int index = e.Index;
                            await AtDequeued(e, target, () => { });
                            passed &= e.Index == index;
                        }
                        break;
                }
                results.Add(new { name = type + "_" + name, passed, lastTime = e.LastTime, index = e.Index, paused = e.Paused });
            }
            catch (Exception ex) { results.Add(new { name = type + "_" + name, passed = false, error = ex.ToString() }); }
            File.WriteAllText(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "engine-cases.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        status.Text = "EngineTests done";
    }
}
