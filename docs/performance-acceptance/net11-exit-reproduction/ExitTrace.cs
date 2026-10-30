using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BiliCopilot.UI;

internal static class ExitTrace
{
    private static readonly object Sync = new();
    [ThreadStatic] private static bool writing;
    private static string file = "";
    private static long sequence;
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    internal static void Initialize(App app)
    {
        file = Path.Combine(Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.Path, "exit-trace.jsonl");
        AppDomain.CurrentDomain.FirstChanceException += (_, e) => Exception("first-chance", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Exception("domain-unhandled", e.ExceptionObject as Exception);
        app.UnhandledException += (_, e) => Exception("xaml-unhandled", e.Exception);
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        queue.ShutdownStarting += (_, _) => Mark("dispatcher-shutdown-starting");
        queue.ShutdownCompleted += (_, _) => Mark("dispatcher-shutdown-completed");
        Mark("trace-initialized");
    }

    internal static void Exception(string stage, Exception? error)
    {
        if (writing) return;
        // No Message, Data, arguments, locals, request bodies, URLs or user identifiers.
        var frames = error == null ? null : new StackTrace(error, true).GetFrames()?.Select(f => new {
            type = f.GetMethod()?.DeclaringType?.FullName, method = f.GetMethod()?.Name,
            line = f.GetFileLineNumber(), il = f.GetILOffset(), native = f.GetNativeOffset()
        }).ToArray();
        Mark(stage, new { type = error?.GetType().FullName, hresult = error?.HResult, frames });
    }

    internal static void Mark(string stage, object? owner = null)
    {
        if (writing || file.Length == 0) return;
        writing = true;
        try
        {
            lock (Sync)
                File.AppendAllText(file, JsonSerializer.Serialize(new {
                    sequence = ++sequence, utc = DateTime.UtcNow, ticks = Stopwatch.GetTimestamp(),
                    pid = Environment.ProcessId, tid = GetCurrentThreadId(),
                    managedThread = Environment.CurrentManagedThreadId, stage, owner
                }) + Environment.NewLine);
        }
        finally { writing = false; }
    }
}
