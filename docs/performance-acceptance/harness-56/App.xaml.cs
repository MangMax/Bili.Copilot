using BiliCopilot.UI;
using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Controls.Popular;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.Pages;
using BiliCopilot.UI.ViewModels.View;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.Models.Constants;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.WinUIKernel.Share;
using RichasyKernel;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
namespace Acceptance56;
public partial class App:Application {
 Window window;Grid slot=new();TextBlock status=new(){Text="Idle"};Button next;
 PopularPage page;PopularMainBody main;PopularSideBody side;PopularPageViewModel vm;Fixture service;ScrollView scroll;
 int step,updates,views,sizes;bool busy;List<WeakReference> oldItems=[];
 readonly ConditionalWeakTable<VideoCardControl,object> seen=new();readonly List<WeakReference> cardRefs=[];
 public App(){InitializeComponent();UnhandledException+=(_,e)=>Trace.Write("unhandled",e.Exception.ToString());}
 protected override void OnLaunched(LaunchActivatedEventArgs args){
  Dependencies.Queue=Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
  var kernelBuilder=RichasyKernel.Kernel.CreateBuilder();kernelBuilder.Services.AddSingleton(Dependencies.Queue);kernelBuilder.Build().InitializeShareKernel(new FixtureLogger());
  window=new(){Title="SYNTHETIC #56 cache observation"};
  var root=new Grid();root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
  var bar=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12,Padding=new Thickness(12)};
  next=new Button{Content="Next"};AutomationProperties.SetAutomationId(next,"Next");next.Click+=async(_,_)=>await Next();bar.Children.Add(next);
  var close=new Button{Content="Close"};AutomationProperties.SetAutomationId(close,"Close");close.Click+=(_,_)=>window.Close();bar.Children.Add(close);
  AutomationProperties.SetAutomationId(status,"Status");bar.Children.Add(status);root.Children.Add(bar);Grid.SetRow(slot,1);root.Children.Add(slot);
  window.Content=root;window.AppWindow.Resize(new(1500,950));window.Activate();
  Trace.Write("launch",new{pid=Environment.ProcessId,runtime=Environment.Version.ToString(),synthetic=true});
 }
 async Task Next(){
  if(busy)return;busy=true;next.IsEnabled=false;status.Text="Running";
  try{
   step++;
   switch(step){
    case 1:
     var discovery=DispatchProxy.Create<IVideoDiscoveryService,Fixture>();service=(Fixture)(object)discovery;
     vm=new(discovery,new FixtureLogger<PopularPageViewModel>());Dependencies.Model=vm;
     vm.VideoListUpdated+=(_,_)=>updates++;
     page=new();slot.Children.Add(page);await Until(()=>Descendants(page).OfType<PopularMainBody>().Any(),"page tree");
     main=Descendants(page).OfType<PopularMainBody>().Single();side=Descendants(page).OfType<PopularSideBody>().Single();
     var inc=(IncrementVerticalScrollControl)main.FindName("View");scroll=(ScrollView)inc.FindName("View");
     scroll.ViewChanged+=(_,_)=>views++;scroll.SizeChanged+=(_,_)=>sizes++;
     await Stable("A-1");Require(vm.Videos.Count==30,"first batch 30");break;
    case 2:await LoadTo(150);await Stable("A-5");break;
    case 3:await LoadTo(450);scroll.ScrollTo(0,scroll.ExtentHeight/2,new(ScrollingAnimationMode.Disabled,ScrollingSnapPointsMode.Ignore));await Stable("A-15-middle");break;
    case 4:scroll.ScrollTo(0,scroll.ExtentHeight,new(ScrollingAnimationMode.Disabled,ScrollingSnapPointsMode.Ignore));await Stable("A-15-bottom");break;
    case 5:await Select(PopularSectionType.Hot);await LoadTo(150);await Stable("B-5");break;
    case 6:await Select(PopularSectionType.Rank);await Stable("C-60");Require(vm.Videos.Count==60,"ranking 60");break;
    case 7:
     await Select(PopularSectionType.Recommend);await Stable("A-return");
     for(int i=1;i<=10;i++)foreach(var section in new[]{PopularSectionType.Hot,PopularSectionType.Rank,PopularSectionType.Recommend}){await Select(section);await Stable($"loop-{i}-{section}");}
     break;
    case 8:page.Visibility=Visibility.Collapsed;await Stable("hidden");break;
    case 9:page.Visibility=Visibility.Visible;await Stable("shown");break;
    case 10:TrackOld();await vm.RefreshVideoCommand.ExecuteAsync(null);await Stable("refresh-A");break;
    case 11:for(int i=1;i<=10;i++){await Task.Delay(1000);Snapshot($"observe-{i}");}break;
    default:throw new InvalidOperationException("Protocol complete");
   }
   status.Text=$"Ready {step}";Trace.Write("step-pass",step);
  }catch(Exception e){Trace.Write("failure",new{step,error=e.ToString()});status.Text=$"FAIL {step}";}
  finally{busy=false;next.IsEnabled=true;}
 }
 async Task Select(PopularSectionType type){
  scroll.ScrollTo(0,0,new(ScrollingAnimationMode.Disabled,ScrollingSnapPointsMode.Ignore));await Stable("before-switch-"+type);
  vm.SelectSectionCommand.Execute(vm.Sections.OfType<PopularSectionItemViewModel>().Single(s=>s.Type==type));
  await Until(()=>Equals(Field(vm,"_displayedSection"),vm.SelectedSection)&&vm.Videos.Count>0&&!vm.IsVideoLoading,"switch complete");
 }
 async Task LoadTo(int count){while(vm.Videos.Count<count){await Until(()=>!vm.IsVideoLoading,"idle before load");int before=vm.Videos.Count;await vm.LoadVideosCommand.ExecuteAsync(null);await Until(()=>!vm.IsVideoLoading,"load completion");Require(vm.Videos.Count>before,"load made progress");}Require(vm.Videos.Count==count,"exact gradient");}
 async Task Until(Func<bool> predicate,string label){var sw=Stopwatch.StartNew();while(!predicate()){if(sw.Elapsed.TotalSeconds>15)throw new TimeoutException(label);await Task.Delay(50);}}
 string Fingerprint()=>string.Join("|",service.Count,updates,views,sizes,vm.Videos.Count,scroll.VerticalOffset,scroll.ExtentHeight,scroll.ViewportHeight,Descendants(main).OfType<VideoCardControl>().Count());
 async Task Stable(string label){
  var sw=Stopwatch.StartNew();string previous=null;double since=0;
  while(true){await Task.Delay(100);string f=Fingerprint();bool ready=!vm.IsVideoLoading&&!vm.IsPartitionLoading&&vm.Videos.Count>0;
   if(page.Visibility==Visibility.Visible)ready &= Descendants(main).OfType<BasicCoverImage>().Where(c=>c.ActualWidth>0).All(c=>!c.IsImageLoading&&Decoded(c));
   if(!ready||f!=previous){previous=f;since=sw.Elapsed.TotalMilliseconds;}
   if(ready&&sw.Elapsed.TotalMilliseconds-since>=1200)break;
   if(sw.Elapsed.TotalSeconds>20)throw new TimeoutException("Not stable/decoded: "+label);
  }
  Snapshot(label);
 }
 static bool Decoded(BasicCoverImage image)=>typeof(Richasy.WinUIKernel.Share.Base.ImageExBase).GetProperty("CanvasImageSource",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(image)!=null;
 static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(o)??throw new MissingFieldException(name);
 static void Require(bool value,string label){if(!value)throw new InvalidOperationException(label);}
 [MethodImpl(MethodImplOptions.NoInlining)] void TrackOld(){oldItems=vm.Videos.Select(v=>new WeakReference(v)).ToList();}
 [MethodImpl(MethodImplOptions.NoInlining)] void Snapshot(string checkpoint){
  var cache=(Dictionary<IPopularSectionItemViewModel,List<VideoItemViewModel>>)Field(vm,"_videoCache");
  var unique=new HashSet<VideoItemViewModel>(ReferenceEqualityComparer.Instance);unique.UnionWith(vm.Videos);foreach(var list in cache.Values)unique.UnionWith(list);
  var cards=Descendants(main).OfType<VideoCardControl>().ToArray();foreach(var c in cards)if(!seen.TryGetValue(c,out _)){seen.Add(c,new());cardRefs.Add(new(c));}
  bool hidden=page.Visibility!=Visibility.Visible;
  bool Visible(VideoCardControl c){if(hidden||c.Visibility!=Visibility.Visible||c.ActualWidth<=0||c.ActualHeight<=0)return false;var r=c.TransformToVisual(scroll).TransformBounds(new(0,0,c.ActualWidth,c.ActualHeight));return r.Right>0&&r.Left<scroll.ViewportWidth&&r.Bottom>0&&r.Top<scroll.ViewportHeight;}
  var visible=cards.Where(Visible).ToArray();using var process=Process.GetCurrentProcess();
  var images=Descendants(main).OfType<BasicCoverImage>().ToArray();
  Trace.Write("sample",new{checkpoint,step,synthetic=true,section=((PopularSectionItemViewModel)vm.SelectedSection).Type.ToString(),items=vm.Videos.Count,
   cache=cache.Select(k=>new{section=((PopularSectionItemViewModel)k.Key).Type.ToString(),count=k.Value.Count,capacity=k.Value.Capacity}).ToArray(),cacheSlots=cache.Values.Sum(v=>v.Count),referenceSlots=vm.Videos.Count+cache.Values.Sum(v=>v.Count),uniqueVMs=unique.Count,uniqueIds=unique.Select(v=>v.Data.Identifier.Id).Distinct().Count(),
   sequenceHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",vm.Videos.Select(v=>v.Data.Identifier.Id))))),
   recommendOffset=Field(vm,"_recommendOffset"),hotOffset=Field(vm,"_hotOffset"),sort="Popular section service order; no sort command",undo="not integrated",
   realizedCards=cards.Length,visibleCards=visible.Length,hiddenRealizedCards=hidden?cards.Length:0,cardWeakTracked=cardRefs.Count,cardWeakAlive=cardRefs.Count(w=>w.IsAlive),oldVMTracked=oldItems.Count,oldVMAlive=oldItems.Count(w=>w.IsAlive),
   visible=visible.Select(c=>new{id=c.ViewModel?.Data.Identifier.Id,y=c.TransformToVisual(scroll).TransformPoint(new(0,0)).Y,height=c.ActualHeight}).ToArray(),
   scroll.VerticalOffset,scroll.ExtentHeight,scroll.ViewportHeight,scroll.ViewportWidth,dpi=page.XamlRoot.RasterizationScale,
   imageCount=images.Length,decodedImages=images.Count(i=>Decoded(i)),imageRequests=BiliCopilot.UI.Extensions.InternalHttpExtensions.Requests,
   requests=service.Count,service.Responses,updates,views,sizes,managedBytes=GC.GetTotalMemory(false),allocatedBytes=GC.GetTotalAllocatedBytes(),gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2),privateBytes=process.PrivateMemorySize64,workingSetBytes=process.WorkingSet64});
 }
 static IEnumerable<DependencyObject> Descendants(DependencyObject p){for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);yield return c;foreach(var d in Descendants(c))yield return d;}}
}
public class FixtureLogger:ILogger {
 public IDisposable BeginScope<TState>(TState state)=>null;public bool IsEnabled(LogLevel level)=>level>=LogLevel.Warning;
 public void Log<TState>(LogLevel level,EventId id,TState state,Exception error,Func<TState,Exception,string> formatter){if(IsEnabled(level))Trace.Write("log",new{level=level.ToString(),message=formatter(state,error),error=error?.ToString()});}
}
public class FixtureLogger<T>:FixtureLogger,ILogger<T>{}
