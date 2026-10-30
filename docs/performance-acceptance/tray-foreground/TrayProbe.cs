using BiliCopilot.UI.Forms;
using Microsoft.UI.Xaml.Automation;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BiliCopilot.UI;

// Only offline composition/observation. MainWindow, TrayLifecycle and tray resources are production bytes.
internal static class TrayProbe
{
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    static MainWindow main=null!;
    static TextBox input=null!;
    static void Write(string kind,object data)=>File.AppendAllText(Path.Combine(Offline48.Folder,"tray-probe.jsonl"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,kind,data})+Environment.NewLine);
    public static void Start()
    {
        Application.Current.UnhandledException+=(_,e)=>Write("unhandled",new{type=e.Exception.GetType().FullName});
        main=new MainWindow();
        var panel=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Spacing=12,Width=440};
        panel.Children.Add(new TextBlock {Text="Offline tray foreground diagnostic"});
        input=new TextBox {Header="Keyboard receipt",PlaceholderText="Test input only"};
        AutomationProperties.SetAutomationId(input,"KeyboardReceipt");panel.Children.Add(input);
        input.TextChanged+=(_,_)=>Write("text",new{text=input.Text,foreground=GetForegroundWindow().ToInt64()});
        input.KeyDown+=(_,e)=>Write("key",new{key=e.Key.ToString(),foreground=GetForegroundWindow().ToInt64()});
        void Button(string id,Action action){var b=new Button{Content=id};AutomationProperties.SetAutomationId(b,id);b.Click+=(_,_)=>action();panel.Children.Add(b);}
        Button("HideOwned",()=>{input.Text="";input.Focus(FocusState.Programmatic);Write("hide-request",new{foreground=GetForegroundWindow().ToInt64()});main.Close();});
        Button("ExitOwned",async()=>{Write("exit-request",new{});await main.RequestExitAsync();});
        ((Grid)main.Content).Children.Add(panel);
        main.Activated+=(_,e)=>Write("activation",new{state=e.WindowActivationState.ToString(),foreground=GetForegroundWindow().ToInt64()});
        main.Activate();((App)Application.Current).SetTrayEnabled(true);
        File.WriteAllText(Path.Combine(Offline48.Folder,"tray-ready.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,hwnd=WinRT.Interop.WindowNative.GetWindowHandle(main).ToInt64()}));
    }
}
