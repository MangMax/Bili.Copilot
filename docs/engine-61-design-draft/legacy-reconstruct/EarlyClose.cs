using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Richasy.Danmaku.Legacy;
using Richasy.Danmaku.Models;
using Richasy.Danmaku.Enums;
namespace SpikeHarness;
public sealed partial class SpikeApp : Application
{
    static readonly object Gate = new();
    internal static void Log(string name, object? data = null) {
        lock(Gate) File.AppendAllText(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path,"events.jsonl"), JsonSerializer.Serialize(new { utc=DateTime.UtcNow, thread=Environment.CurrentManagedThreadId, name, data })+"\n");
    }
    Window window = null!;
    DanmakuFrostMaster? master;
    public SpikeApp() { InitializeComponent(); UnhandledException += (_,e)=>Log("UnhandledException",e.Exception.ToString()); AppDomain.CurrentDomain.UnhandledException += (_,e)=>Log("DomainUnhandled", e.ExceptionObject.ToString()); }
    protected override void OnLaunched(LaunchActivatedEventArgs args) {
        var scene=new Grid { Margin=new Thickness(24,160,24,24) };
        var root=new Grid(); root.Children.Add(scene);
        var status=new TextBlock { Text="IDLE" }; AutomationProperties.SetAutomationId(status,"Status");
        var start=new Button { Content="Run immediate close" }; AutomationProperties.SetAutomationId(start,"Start");
        start.Click += async (_,_)=> {
            start.IsEnabled=false;
            try {
                var sibling=new TextBlock {Text="Owned sibling survives"}; 
                Log("Construct.enter"); master=new DanmakuFrostMaster(scene,logger:new ProbeLogger()); Log("Construct.return");
                var canvas=(Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedControl)scene.Children[scene.Children.Count-1];
                var lateLoaded=0; canvas.Loaded+=(_,_)=> {lateLoaded++; Log("Canvas.Loaded");}; canvas.Unloaded+=(_,_)=>Log("Canvas.Unloaded");
                Log("Canvas.initial",new {canvas.IsLoaded}); scene.Children.Add(sibling);
                master.SetDanmakuEnabledType(true,true,true);
                master.SetDanmakuList(new List<DanmakuItem> {new() { Id="20", Text="TOP20", StartMs=20000, Mode=DanmakuMode.Top, BaseFontSize=40 },new() { Id="21", Text="TOP21", StartMs=21000, Mode=DanmakuMode.Top, BaseFontSize=40 },new() { Id="22", Text="TOP22", StartMs=22000, Mode=DanmakuMode.Top, BaseFontSize=40 }});
                master.Pause(); master.Seek(20000); master.UpdateTime(20000);
                Log("Close.enter"); master.Close(); Log("Close.return");
                Log("ImmediateDetach",new {passed=!scene.Children.Contains(canvas) && scene.Children.Contains(sibling),canvas.IsLoaded,children=scene.Children.Count});
                master.Close(); Log("RepeatedEarlyClose",new {passed=scene.Children.Contains(sibling)});
                await Task.Delay(2000);
                Log("EarlyCloseAfterPump",new {passed=lateLoaded==0 && !canvas.IsLoaded && !scene.Children.Contains(canvas) && scene.Children.Contains(sibling),lateLoaded,canvas.IsLoaded});
                master=new DanmakuFrostMaster(scene,logger:new ProbeLogger());
                var normal=(Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedControl)scene.Children[scene.Children.Count-1];
                scene.Children.Add(sibling); normal.Loaded+=(_,_)=>Log("Normal.Loaded"); normal.Unloaded+=(_,_)=>Log("Normal.Unloaded");
                await Task.Delay(2000);
                Log("NormalLoaded",new {passed=normal.IsLoaded});
                master.Close(); master.Close();
                await Task.Delay(2000);
                Log("NormalCloseAfterPump",new {passed=!normal.IsLoaded && !scene.Children.Contains(normal) && scene.Children.Count==1 && scene.Children.Contains(sibling)});
                Log("SuiteCompleted");
            } catch(Exception ex) { Log("ImmediateCloseReturns",new {passed=false,error=ex.ToString()}); }
            status.Text="DONE";
        };
        var panel=new StackPanel {VerticalAlignment=VerticalAlignment.Top}; panel.Children.Add(status);panel.Children.Add(start);root.Children.Add(panel);
        window=new Window {Title="Owned Ready Review 90c5",Content=root};
        window.Closed+=(_,_)=>{Log("Window.Close.enter");master?.Close();Log("Window.Close.return");};
        window.Activate();
    }
    sealed class ProbeLogger : ILogger<DanmakuFrostMaster> {
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull => null;
        public bool IsEnabled(LogLevel level)=>true;
        public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter)=>SpikeApp.Log("EngineLog",formatter(state,exception));
    }
}
