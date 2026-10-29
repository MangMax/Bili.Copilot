using BiliCopilot.UI;
using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Controls.Popular;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.ViewModels.View;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Media;
using System.Reflection;
namespace Acceptance5053;
public partial class App:Application {
 Window window; Grid slot=new(); TextBlock status=new(){Text="ready",TextWrapping=TextWrapping.Wrap};
 PopularMainBody main;PopularSideBody side;PopularPageViewModel vm;Fixture service;
 IncrementVerticalScrollControl increment;ScrollView scroll;ItemsView sections;
 string scenario="ready";int updates,views,sizes,renders;bool firstRender;
 Dictionary<string,Action> actions=new();
 public App(){InitializeComponent();UnhandledException+=(_,e)=>Trace.Write("unhandled",e.Exception.ToString());}
 protected override void OnLaunched(LaunchActivatedEventArgs args){
  Dependencies.Queue=Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
  window=new(){Title="Offline Popular acceptance 50 53 - 13c29ff"};
  var root=new Grid{Padding=new Thickness(12),RowSpacing=8};
  root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
  var buttons=new StackPanel{Spacing=4};
  var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=4};buttons.Children.Add(row);
  void Add(string id,Action action){if(row.Children.Count==8){row=new(){Orientation=Orientation.Horizontal,Spacing=4};buttons.Children.Add(row);}actions[id]=action;var b=new Button{Content=id};AutomationProperties.SetAutomationId(b,id);b.Click+=(_,_)=>{Trace.Write("action",id);action();};row.Children.Add(b);}
  foreach(var mode in new[]{"Recommend","Hot","Saved","Missing","DirFail","Failure","Rank","Partition","Stale","Long"}){var m=mode;Add(m,()=>New(m));}
  Add("ReleaseDirectory",()=>service.ReleaseDirectory(scenario=="DirFail"));
  Add("Refresh",()=>{service.Page=0;vm.RefreshVideoCommand.Execute(null);});
  Add("SwitchHot",()=>vm.SelectSectionCommand.Execute(vm.Sections.OfType<PopularSectionItemViewModel>().Single(s=>s.Type==PopularSectionType.Hot)));
  Add("ReleaseOld",()=>service.ReleaseOld());
  Add("Detach",()=>slot.Children.Clear());
  Add("Attach",()=>{if(slot.Children.Count==0){slot.Children.Add(side);slot.Children.Add(main);}});
  Add("DelayDetach",()=>{_ = increment.DelayCheckItemsAsync();slot.Children.Clear();});
  Add("Burst",()=>_ = Burst());
  Add("Snapshot",()=>Snapshot("manual"));
  Add("Fence",()=>_ = Fence());
  Add("Close",()=>window.Close());
  root.Children.Add(buttons);Grid.SetRow(status,1);root.Children.Add(status);AutomationProperties.SetAutomationId(status,"Status");
  Grid.SetRow(slot,2);slot.ColumnDefinitions.Add(new(){Width=new GridLength(220)});slot.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});root.Children.Add(slot);
  window.Content=root;window.AppWindow.Resize(new(1660,1060));window.Activate();
  CompositionTarget.Rendering+=(_,_)=>{renders++;if(vm?.Videos.Count>0&&!firstRender && Descendants(main).OfType<VideoCardControl>().Any(c=>c.ActualHeight>0)){firstRender=true;Snapshot("first-card-render");}};
  Trace.Write("launch",new{pid=Environment.ProcessId,identity=Windows.ApplicationModel.Package.Current.Id.Name,baseAssembly=typeof(Richasy.WinUIKernel.Share.Base.LayoutUserControlBase).Assembly.FullName});
 }
 void New(string mode){
  slot.Children.Clear();scenario=mode;updates=views=sizes=0;firstRender=false;
  SettingsToolkit.Saved=mode switch{"Saved" or "Partition"=>"13","Missing"=>"999","DirFail"=>"13","Hot"=>"Hot","Rank"=>"Rank",_=>"Recommend"};
  var discovery=DispatchProxy.Create<IVideoDiscoveryService,Fixture>();service=(Fixture)(object)discovery;service.Scenario=mode;service.Fail=mode=="Failure";service.Hold=mode=="Stale";
  if(mode=="Long"){service.Batch=60;service.Limit=1;}
  vm=new(discovery,NullLogger<PopularPageViewModel>.Instance);Dependencies.Model=vm;
  vm.VideoListUpdated+=(_,_)=>{updates++;Trace.Write("list-updated",new{scenario,updates,count=vm.Videos.Count});};
  main=new();side=new();Grid.SetColumn(main,1);slot.Children.Add(side);slot.Children.Add(main);
  increment=(IncrementVerticalScrollControl)main.FindName("View");scroll=(ScrollView)increment.FindName("View");sections=(ItemsView)side.FindName("SectionView");
  AutomationProperties.SetAutomationId(scroll,"VideoScroll");AutomationProperties.SetAutomationId(sections,"Sections");
  scroll.ViewChanged+=(_,_)=>{views++;Trace.Write("view",new{scenario,views,scroll.VerticalOffset,scroll.ExtentHeight,scroll.ViewportHeight});};
  scroll.SizeChanged+=(_,e)=>{sizes++;Trace.Write("size",new{scenario,sizes,e.NewSize.Width,e.NewSize.Height});};
  main.Loaded+=(_,_)=>Snapshot("main-loaded");main.Unloaded+=(_,_)=>Snapshot("main-unloaded");
  side.Loaded+=(_,_)=>Snapshot("side-loaded");side.Unloaded+=(_,_)=>Snapshot("side-unloaded");
  vm.InitializeCommand.Execute(null);
  if(mode is "Rank" or "Partition" or "Failure" or "Long")service.ReleaseDirectory();
  Snapshot("new");
 }
 async Task Burst(){for(int i=0;i<24;i++){window.AppWindow.Resize(new(1400+(i%4)*60,900+(i%3)*40));scroll.ScrollTo(0,i%2==0?scroll.ExtentHeight:0);await Task.Delay(25);}Snapshot("burst-end");}
 async Task Fence(){Snapshot("fence-start");await Task.Delay(1500);Snapshot("fence-mid");await Task.Delay(3000);Snapshot("fence-end");}
 static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(o);
 int Subscribers(string eventName,Type target)=>((Delegate)Field(vm,eventName))?.GetInvocationList().Count(d=>d.Target?.GetType()==target)??0;
 void Snapshot(string condition){
  if(vm==null)return;
  var cards=Descendants(main).OfType<VideoCardControl>().ToArray();
  var data=new{condition,scenario,requests=service.Count,updates,views,sizes,renders,loaded=main.IsLoaded,scrollLoaded=increment.IsLoaded,active=Field(increment,"_isActive"),pending=Field(increment,"_checkPending"),version=Field(increment,"_loadVersion"),mainSubscriptions=Subscribers("VideoListUpdated",typeof(PopularMainBody)),sideSubscriptions=Subscribers("SectionInitialized",typeof(PopularSideBody)),sideHasSource=sections.ItemsSource!=null,selected=SettingsToolkit.Saved,partitionLoading=vm.IsPartitionLoading,loading=vm.IsVideoLoading,canExecute=vm.LoadVideosCommand.CanExecute(null),items=vm.Videos.Select(v=>v.Data.BvId).ToArray(),cards=cards.Length,visible=cards.Count(c=>{var p=c.TransformToVisual(main).TransformPoint(new(0,0));return p.Y+c.ActualHeight>0&&p.Y<main.ActualHeight;}),scroll.ExtentHeight,scroll.ViewportHeight,scroll.VerticalOffset};
  Trace.Write("state",data);status.Text=$"{scenario} | {condition} | requests {service.Count} | items {vm.Videos.Count} | cards {cards.Length} | updates {updates} | loaded {main.IsLoaded}";
 }
 static IEnumerable<DependencyObject> Descendants(DependencyObject p){for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);yield return c;foreach(var d in Descendants(c))yield return d;}}
}
