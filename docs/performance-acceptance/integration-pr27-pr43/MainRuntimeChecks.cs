using BiliCopilot.UI.Forms;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using RichasyKernel;
using System.Text.Json;

namespace BiliCopilot.UI;

internal static class MainRuntimeChecks
{
    internal static async Task Run()
    {
        var results = new List<object>();
        void Check(bool value, string name)
        {
            results.Add(new { name, status = value ? "PASS" : "FAIL" });
            File.WriteAllText(Path.Combine(Offline48.Folder,"main-results.json"),JsonSerializer.Serialize(results));
            if (!value) throw new Exception(name);
        }
        var app = (App)Application.Current;
        var vm = GlobalDependencies.Kernel.GetRequiredService<AppViewModel>();
        var main = new MainWindow();
        main.Activate();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(main);
        await Task.Delay(1000);
        app.SetTrayEnabled(true);
        main.Close();
        await Task.Delay(400);
        Check(app.TrayLifecycle!.IsResident && !main.AppWindow.IsVisible,"NativeMainCloseHidesToTray");
        app.TrayLifecycle.Restore();
        await Task.Delay(400);
        Check(main.AppWindow.IsVisible && WinRT.Interop.WindowNative.GetWindowHandle(main)==hwnd,"RestoreSameMainHwnd");
        main.Close();
        app.SetTrayEnabled(false);
        Check(main.AppWindow.IsVisible && !app.TrayLifecycle.IsResident,"DisableTrayRestoresHiddenMain");
        app.SetTrayEnabled(true);
        Offline48.Service.Reset(false);
        var player=await Offline48.Open();
        main.Close();
        await player.CloseAsync();
        Check(app.TrayLifecycle.IsResident && !main.AppWindow.IsVisible && vm.Players.Count==0,"ClosingPlayerPreservesIntentionalResidency");
        app.TrayLifecycle.Restore();
        // Keep a real player alive for the explicit-exit path; UI runner invokes this last.
        await Offline48.Open();
        var exit=new Button {Content="Exit main and player"};
        AutomationProperties.SetAutomationId(exit,"ExitMain");
        exit.Click+=async (_,_)=>{Offline48.Log("explicit-exit",new{players=vm.Players.Count});await main.RequestExitAsync();};
        var controller=new Window {Title="Integration 27 43 explicit exit controller",Content=exit};
        controller.Activate();
        File.WriteAllText(Path.Combine(Offline48.Folder,"main-ready.json"),JsonSerializer.Serialize(new{mainHwnd=hwnd.ToInt64(),controllerHwnd=WinRT.Interop.WindowNative.GetWindowHandle(controller).ToInt64(),pid=Environment.ProcessId}));
    }
}
