using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;

static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
if (args[0] == "analyze")
{
    var trace = args[1]; var pid = int.Parse(args[2]);
    var etlx = TraceLog.CreateFromEventPipeDataFile(trace);
    using var log = new TraceLog(etlx);
    var counts = new Dictionary<string,int>();
    var suspendReasons = new Dictionary<string,int>();
    var gcEvents = new List<object>();
    bool gcSuspension = false;
    using var output = new StreamWriter(trace + ".samples.jsonl");
    foreach (var e in log.Events)
    {
        if (e.ProcessID != pid) continue;
        var key = e.ProviderName + "/" + e.EventName;
        counts[key] = counts.GetValueOrDefault(key) + 1;
        if (e.EventName == "GC/SuspendEEStart")
        {
            var reason = e.PayloadByName("Reason")?.ToString() ?? "unknown";
            suspendReasons[reason] = suspendReasons.GetValueOrDefault(reason) + 1;
            gcSuspension = reason == "SuspendForGC";
            if (gcSuspension) gcEvents.Add(new { name=e.EventName, utc=e.TimeStamp.ToUniversalTime(), ms=e.TimeStampRelativeMSec, reason });
        }
        if (e.EventName == "GC/RestartEEStop" && gcSuspension)
        {
            gcEvents.Add(new { name=e.EventName, utc=e.TimeStamp.ToUniversalTime(), ms=e.TimeStampRelativeMSec });
            gcSuspension = false;
        }
        if (e.EventName is "GC/Start" or "GC/Stop")
            gcEvents.Add(new { name=e.EventName, utc=e.TimeStamp.ToUniversalTime(), ms=e.TimeStampRelativeMSec,
                fields=e.PayloadNames.Where(n=>n is "Count" or "Depth" or "Reason" or "Type").Select(n=>new {name=n,value=e.PayloadByName(n)?.ToString()}).ToArray() });
        if (e.ProviderName != "Microsoft-DotNETCore-SampleProfiler") continue;
        var frames = new List<string>();
        for (var s = e.CallStack(); s != null && frames.Count < 100; s = s.Caller)
            frames.Add(s.CodeAddress.FullMethodName);
        SaveSample();
        void SaveSample() => output.WriteLine(JsonSerializer.Serialize(new { utc=e.TimeStamp.ToUniversalTime(), ms=e.TimeStampRelativeMSec, tid=e.ThreadID, name=e.EventName, payload=e.PayloadNames.Select(n=>new {name=n,value=e.PayloadByName(n)?.ToString()}), frames }));
    }
    Save(trace + ".summary.json", new { pid, log.EventsLost, log.SessionStartTime, log.SessionEndTime, counts, suspendReasons, gcEvents });
    return;
}
var targetPid = int.Parse(args[1]);
var layout = Path.GetFullPath(args[2]);
var state = args[3]; var folder = args[4]; var profile = args[0] == "profile";
using var target = Process.GetProcessById(targetPid);
if (targetPid == 9608 || !string.Equals(target.MainModule!.FileName, Path.Combine(layout,"BiliCopilot.UI.exe"), StringComparison.OrdinalIgnoreCase)) throw new Exception("Not owned target");
Directory.CreateDirectory(folder);
var deadline = DateTime.UtcNow.AddMinutes(5);
// The frozen fixture writes only at phase boundaries. Read owned synthetic data only.
while (true)
{
    if (DateTime.UtcNow > deadline || target.HasExited) throw new Exception("Restored boundary unavailable");
    try
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(state,"idle-baseline.json")));
        if (doc.RootElement.GetProperty("rows").GetArrayLength() == 122) break;
    }
    catch (IOException) { }
    catch (JsonException) { }
    await Task.Delay(100);
}
object Snapshot()
{
    target.Refresh();
    var threads = new List<object>();
    foreach (ProcessThread thread in target.Threads)
    {
        try { threads.Add(new { tid=thread.Id, cpuMs=thread.TotalProcessorTime.TotalMilliseconds, userMs=thread.UserProcessorTime.TotalMilliseconds, kernelMs=thread.PrivilegedProcessorTime.TotalMilliseconds }); } catch (InvalidOperationException) { }
    }
    return new { utc=DateTime.UtcNow, cpuMs=target.TotalProcessorTime.TotalMilliseconds, threads };
}
Save(Path.Combine(folder,"before.json"), Snapshot());
EventPipeSession? session = null; Task? copy = null; FileStream? stream = null;
var started = DateTime.UtcNow;
if (profile)
{
    var providers = new[] {
        new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational, 0),
        new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Informational, (long)(ClrTraceEventParser.Keywords.GC | ClrTraceEventParser.Keywords.Jit | ClrTraceEventParser.Keywords.Loader))
    };
    session = new DiagnosticsClient(targetPid).StartEventPipeSession(providers, requestRundown:true, circularBufferMB:64);
    stream = File.Create(Path.Combine(folder,"cpu.nettrace"));
    copy = session.EventStream.CopyToAsync(stream);
}
Save(Path.Combine(folder,"started.json"), new { targetPid, started, attached=DateTime.UtcNow, profile });
await Task.Delay(TimeSpan.FromSeconds(61));
Save(Path.Combine(folder,"after.json"), Snapshot());
if (session != null)
{
    session.Stop(); await copy!.WaitAsync(TimeSpan.FromSeconds(30)); stream!.Dispose(); session.Dispose();
}
Save(Path.Combine(folder,"complete.json"),new { targetPid, profile, ended=DateTime.UtcNow });
