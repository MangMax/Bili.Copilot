#nullable enable
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Richasy.Danmaku.Legacy;
using Richasy.Danmaku.Models;

// Managed-state seam: actual assembly/methods, initialized synchronization fields,
// empty list, null canvas/device/layers. NOT a native/render/lifetime test.
class Boundary
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Read(object o, string n) => o.GetType().GetField(n, F)!.GetValue(o)!;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F)?.SetValue(o, v);
    static void Require(bool b, string s) { if (!b) throw new Exception(s); }
    static Thread Start(Action a) { var t = new Thread(() => a()) { IsBackground = true }; t.Start(); return t; }
    static int Main(string[] args)
    {
        var assembly = typeof(DanmakuFrostMaster).Assembly;
        Console.WriteLine($"Assembly={assembly.Location}; MVID={assembly.ManifestModule.ModuleVersionId}; SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location)))}");
        var m = (DanmakuFrostMaster)RuntimeHelpers.GetUninitializedObject(typeof(DanmakuFrostMaster));
        var render = RuntimeHelpers.GetUninitializedObject(typeof(DanmakuFrostMaster).Assembly.GetType("Richasy.Danmaku.Legacy.DanmakuRender")!);
        Set(render, "_resourcesReady", true); Set(render, "_renderLayerList", Array.CreateInstance(render.GetType().GetNestedType("RenderLayer", BindingFlags.NonPublic)!, 0)); Set(m, "_render", render); Set(m, "_playbackGate", new object());
        Set(m, "_danmakuList", new List<DanmakuItem>());
        var queue = new Queue<uint>(); var signal = new AutoResetEvent(false);
        Set(m, "_updateTimeQueue", queue); Set(m, "_updateEvent", signal); Set(m, "_lastTimeMs", (uint)21000);
        var log = new CallbackLogger();
        typeof(DanmakuFrostMaster).Assembly.GetType("Richasy.Danmaku.Legacy.Logger")!.GetMethod("SetLogger")!.Invoke(null, new object[]{log});
        var entered = new ManualResetEventSlim(); var release = new ManualResetEventSlim();
        int once = 0;
        log.Callback = text => {
            if (text.Contains("Reseek after") && Interlocked.Exchange(ref once, 1) == 0)
            { entered.Set(); Require(release.Wait(5000), "release timeout"); }
        };
        var worker = Start(() => typeof(DanmakuFrostMaster).GetMethod("Updater_DoWork", F)!.Invoke(m, new object?[]{null}));
        m.UpdateTime(20000); Require(entered.Wait(3000), "worker did not reach reseek");
        if (args[0] == "throwstop" || args[0] == "throwclose")
        {
            bool closing = args[0] == "throwclose";
            var expected = new Exception("logger sentinel");
            log.Callback = text => { if(text.Contains("DanmakuRender is stopped")) throw expected; };
            m.UpdateTime(22000);
            try { if(closing)m.Close();else m.Stop();throw new Exception("missing logger exception"); }
            catch(Exception e) when(ReferenceEquals(e,expected)) {}
            Require((bool)Read(render,"_isStopped") && queue.Count==0,"state lost on logger exception");
            Require((bool)Read(m,"_isClosing")==closing,"closing state lost");
            log.Callback = null; release.Set(); m.Close(); Require(worker.Join(3000),"worker cleanup");
            Console.WriteLine($"{args[0]}: PASS exact exception propagated; stopped=true queue=0 closing={closing}");return 0;
        }
        if (args[0] == "reentry" || args[0] == "closereentry")
        {
            // Stop invokes logger on this thread while worker owns list. The callback
            // synchronously calls the public Seek API. No cross-thread UI dispatch.
            int stopCallback = 0;
            log.Callback = text => {
                if (text.Contains("DanmakuRender is stopped") && Interlocked.Exchange(ref stopCallback, 1) == 0)
                { release.Set(); m.Seek(19000); }
            };
            var stop = Start(args[0] == "closereentry" ? m.Close : m.Stop);
            bool completed = stop.Join(1500);
            Console.WriteLine($"{(args[0] == "closereentry" ? "CloseLoggerCallsSeek" : "StopLoggerCallsSeek")}: completed={completed}; callbackEntered={stopCallback>0}; stopThread={stop.ThreadState}; workerThread={worker.ThreadState}; candidate={typeof(DanmakuFrostMaster).GetField("_playbackGate",F)!=null}");
            if (completed) { m.Close(); Require(worker.Join(3000), "worker cleanup"); }
            if (completed && args[0] == "closereentry") Require((uint)Read(m,"_lastTimeMs")==21000 && (bool)Read(render,"_isStopped") && queue.Count==0,"close reentry changed state");
            // Candidate deadlock threads are background and own no native resources.
            // Process exit ends them; no force-kill, dump, or native cleanup claim.
            return completed ? 0 : 2;
        }
        if (args[0] == "stop")
        {
            m.Stop(); Require((bool)Read(render,"_isStopped"), "Stop failed before release");
            release.Set(); Require(SpinWait.SpinUntil(()=> (uint)Read(m,"_lastTimeMs")==20000,3000), "seek missing");
            Console.WriteLine($"DequeuedThenStop: stoppedAfterSeek={Read(render,"_isStopped")}; time={Read(m,"_lastTimeMs")}");
        }
        else if (args[0] == "queue")
        {
            m.UpdateTime(22000); m.UpdateTime(21000); m.Pause(); m.Resume(); m.Pause(); release.Set();
            bool drained = SpinWait.SpinUntil(()=> { lock(queue) return queue.Count==0 && (uint)Read(m,"_lastTimeMs")==21000; },3000);
            Require(drained, "queued tail lost or wakeup lost");
            Require((bool)Read(m,"_pauseRequested"), "latest Pause lost");
            Console.WriteLine("QueuedTailAndLatestIntent: PASS time=21000 queue=0 pauseRequested=true");
        }
        else if (args[0] == "close")
        {
            m.UpdateTime(22000); m.Close(); m.Resume(); release.Set();
            Require(worker.Join(3000), "worker cleanup");
            Require((uint)Read(m,"_lastTimeMs")==21000 && (bool)Read(render,"_isStopped") && queue.Count==0,"late close commit");
            Console.WriteLine("CloseRejectsLateCommit: PASS time=21000 stopped=true queue=0 workerExited=true");
        }
        m.Close(); Require(worker.Join(3000), "worker cleanup"); signal.Dispose(); return 0;
    }
    sealed class CallbackLogger : ILogger<DanmakuFrostMaster>
    {
        public Action<string>? Callback;
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull => null;
        public bool IsEnabled(LogLevel l)=>true;
        public void Log<TState>(LogLevel l, EventId id, TState s, Exception? ex, Func<TState,Exception?,string> f)=>Callback?.Invoke(f(s,ex));
    }
}
