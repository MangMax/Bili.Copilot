using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;

// Live stream only: never persist EventPipe payloads, URLs, headers or bodies.
var pid = int.Parse(args[0]);
var prefix = Path.GetFullPath(args[1]);
const string ownedExe = @"C:\Users\hp\.t3\worktrees\Bili.Copilot\t3-1630ecee\artifacts\full-app-duration-fix\layout\BiliCopilot.UI.exe";
using var process = Process.GetProcessById(pid);
if (pid == 9608 || !string.Equals(process.MainModule!.FileName, ownedExe, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Ownership mismatch");
using var session = new DiagnosticsClient(pid).StartEventPipeSession(
    new[] { new EventPipeProvider("System.Net.Http", EventLevel.Verbose, -1L) }, false, 32);
using var source = new EventPipeEventSource(session.EventStream);
using var writer = new StreamWriter(prefix + ".jsonl") { AutoFlush = true };
var active = new Dictionary<Guid, (string Kind, int Sequence)>();
var sequence = 0;
var eventNames = new HashSet<string>();
source.Dynamic.All += e => {
    if (e.ProviderName != "System.Net.Http") return;
    eventNames.Add(e.EventName);
    var eventName = e.EventName.Replace("/", "");
    if (eventName == "RequestStart") {
        var host = e.PayloadByName("host") as string;
        var raw = e.PayloadByName("pathAndQuery") as string ?? "";
        var end = raw.IndexOf('?');
        var path = raw.AsSpan(0, end < 0 ? raw.Length : end);
        string kind = null;
        if (host == "app.bilibili.com" && path.SequenceEqual("/x/v2/feed/index")) kind = "recommend";
        if (host == "grpc.biliapi.net" && path.SequenceEqual("/bilibili.app.show.v1.Popular/Index")) kind = "hot";
        if (host == "grpc.biliapi.net" && path.SequenceEqual("/bilibili.app.show.v1.Rank/RankRegion")) kind = "rank";
        if (kind is null) return;
        active[e.ActivityID] = (kind, ++sequence);
        writer.WriteLine(JsonSerializer.Serialize(new { utc=e.TimeStamp.ToUniversalTime(), phase="start", kind, sequence, activity=e.ActivityID }));
    } else if (eventName == "RequestStop" && active.Remove(e.ActivityID, out var request)) {
        writer.WriteLine(JsonSerializer.Serialize(new { utc=e.TimeStamp.ToUniversalTime(), phase="stop", kind=request.Kind, sequence=request.Sequence, activity=e.ActivityID, status=Convert.ToInt32(e.PayloadByName("statusCode")) }));
    } else if (eventName == "RequestFailed" && active.TryGetValue(e.ActivityID, out var failed)) {
        writer.WriteLine(JsonSerializer.Serialize(new { utc=e.TimeStamp.ToUniversalTime(), phase="failed", kind=failed.Kind, sequence=failed.Sequence, activity=e.ActivityID }));
    }
};
var processing = Task.Run(() => source.Process());
File.WriteAllText(prefix + ".ready", "Owned PID; HTTP whitelist; live parsing; no raw trace; no forced GC");
var timer = Stopwatch.StartNew();
while (!File.Exists(prefix + ".stop") && !process.HasExited && timer.Elapsed.TotalMinutes < 45) await Task.Delay(200);
session.Stop();
await processing;
File.WriteAllText(prefix + ".summary.json", JsonSerializer.Serialize(new { pid, starts=sequence, unmatched=active.Count, lost=source.EventsLost, eventNames }));
