using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace BiliCopilot.UI;

internal static class IdleBaseline
{
    internal static async Task Measure(MainWindow main)
    {
        var app = (App)Application.Current;
        T? Find<T>(DependencyObject node) where T : DependencyObject
        {
            if (node is T match) return match;
            for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var found = Find<T>(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
                if (found is not null) return found;
            }
            return null;
        }
        var root = Find<RootLayout>(main.Content) ?? throw new Exception("Root layout absent");
        var frame = (MainFrame)root.FindName("MainFrame");
        frame.NavigateTo(typeof(SettingsPage));
        var host = (Window)typeof(Offline48).GetField("Host", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        host.AppWindow.Hide();
        await Task.Delay(5000);
        if (frame.GetCurrentContent() is not SettingsPage) throw new Exception("Baseline page mismatch");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(main);
        var rows = new List<object>();
        using var process = Process.GetCurrentProcess();
        foreach (var phase in new[] { "visible", "hidden", "restored" })
        {
            if (phase == "hidden") main.Close();
            if (phase == "restored") app.TrayLifecycle!.Restore();
            await Task.Delay(2000);
            for (var i = 0; i <= 60; i++)
            {
                process.Refresh();
                rows.Add(new { phase, sample = i, utc = DateTimeOffset.UtcNow, cpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds,
                    privateBytes = process.PrivateMemorySize64, workingSet = process.WorkingSet64, handles = process.HandleCount,
                    threads = process.Threads.Count, visible = main.AppWindow.IsVisible, hwnd = hwnd.ToInt64(),
                    scale = root.XamlRoot.RasterizationScale });
                if (i < 60) await Task.Delay(1000);
            }
            File.WriteAllText(Path.Combine(Offline48.Folder, "idle-baseline.json"), JsonSerializer.Serialize(new {
                condition = "offline full-source host; real SettingsPage; no player/download; notifications disabled before startup; controller hidden; warm 5s; no forced GC or trim",
                source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "source-provenance.txt")).Trim(), rows }));
        }
        for (var i = 0; i < 20; i++)
        {
            main.Close(); await Task.Delay(100);
            if (main.AppWindow.IsVisible) throw new Exception("Cycle did not hide");
            app.TrayLifecycle!.Restore(); await Task.Delay(100);
            if (!main.AppWindow.IsVisible || WinRT.Interop.WindowNative.GetWindowHandle(main) != hwnd) throw new Exception("Cycle changed main window");
        }
        Offline48.Log("idle-cycles", new { count = 20, sameHwnd = true });
        host.AppWindow.Show();
    }
}
