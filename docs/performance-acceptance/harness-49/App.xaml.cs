using BiliCopilot.UI.Controls.Danmaku;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.Models.Constants;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Models.Danmaku;
using Richasy.Danmaku;
using Richasy.Danmaku.Legacy;
using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
namespace Acceptance49;
public partial class App:Application {
 Window window; Grid slot=new(); TextBlock status=new(); VideoDanmakuPanel panel; DanmakuRenderViewModel vm;
 int serial, loads, unloads; bool busy;
 readonly string log=Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path,"events.jsonl");
 public App(){InitializeComponent();UnhandledException+=(_,e)=>Write("UNHANDLED",new{error=e.Exception.ToString()});}
 protected override void OnLaunched(LaunchActivatedEventArgs args){
  window=new Window{Title="Acceptance 49 C4D3 — actual VideoDanmakuPanel"};
  var root=new Grid{Padding=new Thickness(12),RowSpacing=8};
  root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
  var commands=new StackPanel{Spacing=4};root.Children.Add(commands);
  void Add(string name,Action action){
   if(commands.Children.Count==0||((StackPanel)commands.Children.Last()).Children.Count==8)commands.Children.Add(new StackPanel{Orientation=Orientation.Horizontal,Spacing=4});
   var b=new Button{Content=name};AutomationProperties.SetAutomationId(b,name);
   b.Click+=async(_,_)=>{if(busy)return;busy=true;var sw=Stopwatch.StartNew();Write("action",new{name});try{action();await Fence();if(name=="Remount"){var mounted=Stopwatch.StartNew();while(!panel.IsLoaded&&mounted.ElapsedMilliseconds<3000)await Task.Delay(20);await Fence();if(!panel.IsLoaded)throw new Exception("mount timeout");}if(name=="DirectX"||name=="Win2D"){var deadline=Stopwatch.StartNew();while(Field(panel,"_danmakuRenderer")==null&&Field(panel,"_danmakuMaster")==null&&deadline.ElapsedMilliseconds<3000)await Task.Delay(20);if(Field(panel,"_danmakuRenderer")==null&&Field(panel,"_danmakuMaster")==null)throw new Exception("renderer readiness timeout");}Snapshot(name,sw.Elapsed.TotalMilliseconds);}catch(Exception e){Write("ERROR",new{name,error=e.ToString()});status.Text="ERROR "+name;}finally{busy=false;}};
   ((StackPanel)commands.Children.Last()).Children.Add(b);
  }
  Add("DirectX",()=>Fresh(DanmakuRendererType.DirectX));Add("Win2D",()=>Fresh(DanmakuRendererType.Win2D));
  Add("Burst",()=>{for(int i=0;i<100;i++){vm.Renderer=i%2==0?DanmakuRendererType.DirectX:DanmakuRendererType.Win2D;vm.Redraw();}vm.Renderer=engine;vm.Initialize(Items("LATEST"));vm.UpdatePosition(2);vm.Redraw();});
  Add("Content",()=>{vm.Initialize(Items("REPLACEMENT"));vm.UpdatePosition(2);});
  Add("Style",()=>{vm.DanmakuOpacity=1;vm.DanmakuFontSize=2;vm.IsDanmakuBold=false;vm.ResetStyle();});
  Add("Pause",()=>vm.Pause());Add("Resume",()=>vm.Resume());
  Add("SeekForward",()=>vm.UpdatePosition(20));Add("SeekBack",()=>vm.UpdatePosition(2));
  Add("Redraw",()=>vm.Redraw());Add("Hide",()=>vm.IsShowDanmaku=false);Add("Show",()=>vm.IsShowDanmaku=true);
  Add("Clear",()=>{vm.Redraw();vm.ClearAll();});Add("Reinitialize",()=>{vm.Initialize(Items("REINIT"));vm.UpdatePosition(2);vm.Resume();});
  Add("ReplaceVM",()=>{var old=vm;vm=NewModel(engine,"NEWVM");panel.ViewModel=vm;old.Initialize(Items("OLD-POISON"));old.UpdatePosition(99);old.Redraw();old.Pause();});
  Add("UnloadQueued",()=>{vm.Redraw();vm.Renderer=engine==DanmakuRendererType.DirectX?DanmakuRendererType.Win2D:DanmakuRendererType.DirectX;slot.Children.Clear();});
  Add("DetachedUpdates",()=>{vm.Initialize(Items("DETACHED"));vm.UpdatePosition(20);vm.Pause();vm.Redraw();});
  Add("Remount",()=>{if(slot.Children.Count==0)slot.Children.Add(panel);});
  Add("Realtime",()=>vm.AddDanmaku("PUBLIC VM REALTIME"));Add("Switch",()=>{engine=engine==DanmakuRendererType.DirectX?DanmakuRendererType.Win2D:DanmakuRendererType.DirectX;vm.Renderer=engine;});Add("Sample",()=>{});Add("Close",()=>window.Close());
  AutomationProperties.SetAutomationId(status,"Status");Grid.SetRow(status,1);root.Children.Add(status);
  slot.Background=new SolidColorBrush(Microsoft.UI.Colors.Black);Grid.SetRow(slot,2);root.Children.Add(slot);
  window.Content=root;window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1400,820));window.Activate();
  Write("launch",new{pid=Environment.ProcessId,identity=Windows.ApplicationModel.Package.Current.Id.Name});
  status.Text="ready";
 }
 DanmakuRendererType engine;
 static List<DanmakuInformation> Items(string prefix)=>new(){
  new("1",prefix+" TOP @1.999",5,1.999,0xFFFFFF,30),new("2",prefix+" ROLL @1.999",1,1.999,0x00FFFF,30),
  new("3",prefix+" TOP @19.999",5,19.999,0xFFFFFF,30),new("4",prefix+" ROLL @19.999",1,19.999,0xFFFF00,30),new("5",prefix+" FUTURE @60",5,60,0xFFFFFF,30)};
 DanmakuRenderViewModel NewModel(DanmakuRendererType type,string prefix){var m=new DanmakuRenderViewModel();m.Renderer=type;m.Initialize(Items(prefix));m.UpdatePosition(2);m.Resume();return m;}
 void Fresh(DanmakuRendererType type){slot.Children.Clear();engine=type;vm=NewModel(type,"BASE");panel=new VideoDanmakuPanel{ViewModel=vm};panel.Loaded+=(_,_)=>{loads++;Write("loaded",new{loads});};panel.Unloaded+=(_,_)=>{unloads++;Write("unloaded",new{unloads});};slot.Children.Add(panel);}
 async Task Fence(){var t=new TaskCompletionSource();if(!window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,()=>t.TrySetResult()))throw new Exception("enqueue rejected");await t.Task.WaitAsync(TimeSpan.FromSeconds(5));}
 static object Field(object o,string name)=>o?.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(o);
 static object Fields(object o)=>o==null?null:o.GetType().GetFields(BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Public).Where(f=>f.FieldType.IsPrimitive||f.FieldType==typeof(TimeSpan)||f.FieldType==typeof(Stopwatch)).ToDictionary(f=>f.Name,f=>f.GetValue(o) is Stopwatch s?(object)new{elapsed=s.Elapsed.TotalMilliseconds,s.IsRunning}:f.GetValue(o));
 static object Tree(object o,int depth){if(o==null)return null;return o.GetType().GetFields(BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Public).ToDictionary(f=>f.Name,f=>{var v=f.GetValue(o);if(v is Stopwatch sw)return (object)new{ms=sw.Elapsed.TotalMilliseconds,sw.IsRunning};if(v==null||f.FieldType.IsPrimitive||f.FieldType==typeof(string)||f.FieldType==typeof(TimeSpan))return v;if(depth<2&&f.FieldType.Namespace?.StartsWith("Richasy.Danmaku")==true)return Tree(v,depth+1);return (object)f.FieldType.FullName;});}
 void Snapshot(string name,double elapsed){
  var dx=Field(panel,"_danmakuRenderer") as DanmakuRenderer;var legacy=Field(panel,"_danmakuMaster") as DanmakuFrostMaster;
  var grid=panel?.FindName("RootGrid") as Grid;
  var data=new{ name,elapsed,loaded=panel?.IsLoaded,loads,unloads,engine=vm?.Renderer.ToString(),paused=vm?.IsPaused,show=vm?.IsShowDanmaku,position=vm?.Position,
   dx=dx!=null,legacy=legacy!=null,visuals=grid?.Children.Count,visibility=grid?.Visibility.ToString(),lastProgress=Field(panel,"_lastProgress"),
   items=(dx?.GetDanmakuList()??legacy?.GetDanmakuList())?.Select(i=>new{i.Text,i.StartMs}).ToArray(),
   renderer=Fields(dx),engineTree=Tree(dx??(object)legacy,0),scheduler=Fields(Field(dx,"_scheduler")),legacyState=Fields(legacy),session=Fields(Field(panel,"_redrawSession"))};
  Write("snapshot",data);status.Text=$"{++serial} {name} ready engine={vm?.Renderer} paused={vm?.IsPaused} pos={vm?.Position} loaded={panel?.IsLoaded} visuals={grid?.Children.Count}";
 }
 void Write(string kind,object data)=>File.AppendAllText(log,JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,kind,data})+Environment.NewLine);
}
