using System.Reflection;
using System.Text.Json;
using BiliCopilot.UI.Controls.Core;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Dispatching;
using RichasyKernel;

namespace BiliCopilot.UI;

internal static class TimerChecks
{
    internal static readonly Dictionary<PlayerInteractivePanel, int> Reads = new();
    internal static void BeforeRead(PlayerInteractivePanel panel)
    {
        Reads[panel] = Reads.GetValueOrDefault(panel) + 1;
        ExitTrace.Mark("timer-vm-read", new { id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(panel) });
    }

    internal static async Task Run(Panel host)
    {
        var results = new List<object>();
        void Check(string name, bool ok, object detail)
        {
            results.Add(new { name, status = ok ? "PASS" : "FAIL", detail });
            File.WriteAllText(Path.Combine(Offline48.Folder, "timer-results.json"), JsonSerializer.Serialize(results));
        }
        var vm = GlobalDependencies.Kernel.GetRequiredService<PlayerViewModel>();
        var panel = new PlayerInteractivePanel { ViewModel = vm, Width = 100, Height = 100 };
        var timer = (DispatcherQueueTimer)typeof(PlayerInteractivePanel).GetField("_tempControlTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel)!;
        var tick = typeof(PlayerInteractivePanel).GetMethod("OnTempControlTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var loaded = 0; var unloaded = 0;
        panel.Loaded += (_, _) => { loaded++; ExitTrace.Mark("test-loaded", new { loaded, running = timer.IsRunning }); };
        panel.Unloaded += (_, _) => { unloaded++; ExitTrace.Mark("test-unloaded", new { unloaded, running = timer.IsRunning }); };
        try
        {
            for (var cycle = 1; cycle <= 2; cycle++)
            {
                host.Children.Add(panel);
                await Task.Delay(1100);
                Check($"LoadedTimerRuns{cycle}", loaded == cycle && timer.IsRunning, new { loaded, running = timer.IsRunning });
                var before = Reads.GetValueOrDefault(panel);
                await Task.Delay(600);
                Check($"LoadedTimerTicks{cycle}", Reads.GetValueOrDefault(panel) > before, new { before, after = Reads.GetValueOrDefault(panel) });
                host.Children.Remove(panel);
                await Task.Delay(100);
                Check($"UnloadedTimerStops{cycle}", unloaded == cycle && !timer.IsRunning, new { unloaded, running = timer.IsRunning });
                before = Reads.GetValueOrDefault(panel);
                await Task.Delay(1100);
                Check($"NoReadAfterUnload{cycle}", Reads.GetValueOrDefault(panel) == before, new { before, after = Reads.GetValueOrDefault(panel) });
            }
            await vm.CloseAsync();
            var reads = Reads.GetValueOrDefault(panel);
            tick.Invoke(panel, new object[] { timer, new object() });
            Check("LateTickDoesNotReadClosedVm", Reads.GetValueOrDefault(panel) == reads, new { before = reads, after = Reads.GetValueOrDefault(panel) });
        }
        finally
        {
            host.Children.Remove(panel);
            timer.Stop();
            File.WriteAllText(Path.Combine(Offline48.Folder, "timer-done.json"), "{}");
        }
    }
}
