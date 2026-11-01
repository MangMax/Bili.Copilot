using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml.Media;
using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.Models.Constants;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.Base.Repeater;
using Windows.Storage;
namespace Acceptance5053;
public partial class App {
 async Task RunContinuity() {
  if(matrixRunning)return; matrixRunning=true;
  var output=ApplicationData.Current.LocalFolder.Path;
  try {
   Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("ms-appx:///Repeater.xaml")});
   ImageExBase.EnableBackgroundDecoding=false; UserItemViewModel.UniqueAvatars=true;
   var requestedFamily=File.ReadAllText(Path.Combine(output,"related-family.txt")).Trim();
   var returnOnly=requestedFamily.EndsWith("Return");
   var realistic=requestedFamily.Contains("Real");
   var family=requestedFamily.Replace("Return","").Replace("Real","");
   ArticleItemViewModel.Realistic=SeasonItemViewModel.Realistic=realistic;
   var cache=double.Parse(File.ReadAllText(Path.Combine(output,"mount-cache.txt")),System.Globalization.CultureInfo.InvariantCulture);
   foreach(var count in new[]{50,500}) {
    slot.Children.Clear(); window.AppWindow.Resize(new(1400,900));
    object Item(int i)=>family switch {"User"=>new UserItemViewModel(i),"Live"=>new LiveItemViewModel(),"Article"=>new ArticleItemViewModel(i),"Season"=>new SeasonItemViewModel(i),_=>new VideoItemViewModel(new VideoInformation(new($"v-{i}","Fixture",null),null,bvId:$"Video {i}"),VideoCardStyle.Recommend)};
    var items=new ObservableCollection<object>(Enumerable.Range(0,count).Select(Item));
    var key=family switch{"User"=>"UserLayout","Live"=>"LiveLayout","Article"=>"ArticleLayout","Season"=>"PgcLayout",_=>"VideoLayout"};
    var owner=new AppRepeater{ItemsSource=items,ItemTemplate=(DataTemplate)new MatrixTemplates()[family=="Season"?"SeasonFixed":family],Layout=(VirtualizingLayout)Resources[key]};
    RepeaterCachePolicy.SetVerticalCacheLength(owner,cache);
    var view=new IncrementVerticalScrollControl{Element=owner};Grid.SetRowSpan(view,2);Grid.SetColumnSpan(view,2);slot.Children.Add(view);
    await Task.Delay(2000);
    var sv=(ScrollView)view.FindName("View");var ir=(ItemsRepeater)owner.FindName("Repeater");
    // Warm the exact region before measuring repeated forward/reverse motion.
    for(int i=0;i<10;i++){await ScrollAndWait(sv,Math.Min(sv.ScrollableHeight,i*sv.ViewportHeight/2));await Task.Delay(100);}
    await ScrollAndWait(sv,0);await Task.Delay(1000);
    var startedUtc=DateTimeOffset.UtcNow;var clock=Stopwatch.StartNew(); var samples=new List<object>();var pixels=new List<object>();
    var phase="scroll"; int step=-1; int prepared=0;ir.ElementPrepared+=(_,_)=>prepared++;
    object Sample(string trigger) {
     var rows=new List<object>();int realized=0;var intervals=new List<(double a,double b)>();
     for(int i=0;i<count;i++) {
      if(ir.TryGetElement(i) is not FrameworkElement e)continue;realized++;
      var p=e.TransformToVisual(sv).TransformPoint(new(0,0));
      if(p.Y+e.ActualHeight<=0||p.Y>=sv.ViewportHeight)continue;
      intervals.Add((Math.Max(0,p.Y),Math.Min(sv.ViewportHeight,p.Y+e.ActualHeight)));
      var card=Descendants(e).OfType<FrameworkElement>().FirstOrDefault(c=>c is UserCardControl or VideoCardControl or LiveCardControl or ArticleCardControl or SeasonCardControl);
      var actual=card?.GetType().GetProperty("ViewModel")?.GetValue(card);
      var images=card==null?Array.Empty<ImageExBase>():Descendants(card).OfType<ImageExBase>().ToArray();
      var expectedUris=new[]{"Cover","Avatar"}.Select(name=>items[i].GetType().GetProperty(name)?.GetValue(items[i]) as Uri).Where(uri=>uri!=null).ToArray();
      rows.Add(new{index=i,x=p.X,y=p.Y,w=e.ActualWidth,h=e.ActualHeight,vmCorrect=ReferenceEquals(actual,items[i]),vmNull=actual==null,images=images.Select(im=>new{source=im.Source?.ToString(),sourceCorrect=im.Source!=null&&expectedUris.Contains(im.Source),ready=HasImage(im)}).ToArray()});
     }
     double covered=0,end=0;foreach(var v in intervals.OrderBy(v=>v.a)){covered+=Math.Max(0,v.b-Math.Max(end,v.a));end=Math.Max(end,v.b);}
     return new{ms=clock.Elapsed.TotalMilliseconds,phase,step,trigger,offset=sv.VerticalOffset,height=sv.ViewportHeight,extent=sv.ExtentHeight,coverage=covered/sv.ViewportHeight,realized,prepared,draws=ListProbe.Draws,rows};
    }
    EventHandler<object> rendering=(_,_)=>samples.Add(Sample("rendering"));
    CompositionTarget.Rendering+=rendering;
    // External winapp recording observes compositor motion independently of UI stalls.
    var trajectory=new List<object>();
    for(step=0;step<(returnOnly?32:64);step++){
     if(returnOnly) {
      // Both endpoints are inside the already-rendered warmup region.
      var target=step%2==0?Math.Min(sv.ScrollableHeight,3*sv.ViewportHeight):0;
      trajectory.Add(new{step,ms=clock.Elapsed.TotalMilliseconds,target});
      sv.ScrollTo(0,target,new ScrollingScrollOptions(ScrollingAnimationMode.Enabled,ScrollingSnapPointsMode.Ignore));
      samples.Add(Sample("request"));await Task.Delay(150);continue;
     }
     // Native inertia continues on the compositor while realization runs on the UI thread.
     var velocity=step%16<8?4000f:-4000f;
     trajectory.Add(new{step,ms=clock.Elapsed.TotalMilliseconds,velocity});
     sv.AddScrollVelocity(new System.Numerics.Vector2(0,velocity),new System.Numerics.Vector2(.95f,.95f));
     samples.Add(Sample("request"));await Task.Delay(50);
    }
    await Task.Delay(500);CompositionTarget.Rendering-=rendering;
    var memory=Process.GetCurrentProcess();
    var beats=new List<double>();var previous=clock.Elapsed.TotalMilliseconds;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};
    timer.Tick+=(_,_)=>{var now=clock.Elapsed.TotalMilliseconds;beats.Add(now-previous);previous=now;};
    phase="resize";timer.Start();
    for(step=0;step<120;step++){window.AppWindow.Resize(new(1200+(step%20)*20,900));await Task.Delay(16);}
    await Task.Delay(800);timer.Stop();
    File.WriteAllText(Path.Combine(output,$"matrix-continuity-{count}.json"),JsonSerializer.Serialize(new{family,returnOnly,realistic,count,startedUtc,cache=ir.VerticalCacheLength,dpi=sv.XamlRoot.RasterizationScale,trajectory,samples,pixels,beats,workingSet=memory.WorkingSet64,privateBytes=memory.PrivateMemorySize64}));
   }
  }catch(Exception e){File.WriteAllText(Path.Combine(output,"matrix-error.txt"),e.ToString());}
  File.WriteAllText(Path.Combine(output,"matrix-finished.txt"),DateTimeOffset.UtcNow.ToString("O"));
 }
}
