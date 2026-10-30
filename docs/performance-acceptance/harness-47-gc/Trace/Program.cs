using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;

if (args[0] == "probe") {
    File.WriteAllText(args[1] + ".pid", Environment.ProcessId.ToString());
    while (!File.Exists(args[1] + ".ready")) await Task.Delay(50);
    var before = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
    var clock = Stopwatch.StartNew();
    long bytes = 0;
    while (clock.Elapsed.TotalSeconds < 3) {
        for (int i = 0; i < 1000; i++) { var b = new byte[32000]; b[0] = 1; GC.KeepAlive(b); bytes += b.Length; }
        await Task.Delay(10);
    }
    File.WriteAllText(args[1] + ".probe.json", JsonSerializer.Serialize(new { before, after = Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray(), bytes }));
    File.WriteAllText(args[1] + ".stop", "done");
    await Task.Delay(2000);
    return;
}
if (args[0] == "collect") {
    int pid = int.Parse(args[1]); string prefix = Path.GetFullPath(args[2]);
    var p = Process.GetProcessById(pid);
    if (pid == 9608 || !p.MainModule.FileName.StartsWith(Path.GetFullPath(args[3]), StringComparison.OrdinalIgnoreCase)) throw new Exception("Ownership path guard");
    using var session = new DiagnosticsClient(pid).StartEventPipeSession(new[] { new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Informational, 1) }, requestRundown: false, circularBufferMB: 64);
    using var file = File.Create(prefix + ".nettrace");
    var copy = session.EventStream.CopyToAsync(file);
    File.WriteAllText(prefix + ".ready", "GC keyword 1 informational; 64 MiB buffer; rundown false; no heap dump/forced collection");
    var clock = Stopwatch.StartNew();
    while (!File.Exists(prefix + ".stop") && clock.Elapsed.TotalMinutes < 10) await Task.Delay(100);
    session.Stop(); await copy; await file.FlushAsync();
    return;
}
if (args[0] == "parse") {
    var rows = new List<object>();
    using var source = new EventPipeEventSource(args[1]);
    source.Clr.All += e => {
        if (e.EventName.StartsWith("GC/")) rows.Add(new { name = e.EventName, ms = e.TimeStampRelativeMSec, utc = e.TimeStamp.ToUniversalTime(), pid = e.ProcessID, tid = e.ThreadID, data = e.PayloadNames.ToDictionary(n => n, n => e.PayloadByName(n)?.ToString()) });
    };
    source.Process();
    File.WriteAllText(args[2], JsonSerializer.Serialize(new { lost = source.EventsLost, events = rows }, new JsonSerializerOptions { WriteIndented = true }));
}
