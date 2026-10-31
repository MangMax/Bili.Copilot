using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using BiliCopilot.UI;
using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.Models.Constants;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.Base.Repeater;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Acceptance5053;
public static class ListProbe { public static int Measures, Arranges, Draws; }
public partial class App
{
    bool matrixRunning;
    async Task RunMatrix(bool green, bool extra=false)
    {
        if(matrixRunning) return;
        matrixRunning=true;
        var output=ApplicationData.Current.LocalFolder.Path;
        var rows=new List<object>();
        var checks=new List<string>();
        try
        {
            slot.Children.Clear();
            ImageExBase.EnableBackgroundDecoding=false;
            if(green) Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("ms-appx:///Repeater.xaml")});
            var templates=new MatrixTemplates();
            foreach(var family in extra?new[]{"Moment","Season","Notify"}:new[]{"Recommend","Hot","Partition","Search","Live","Article","User","Stack"})
            foreach(var count in new[]{50,500})
            {
                status.Text=$"{(green?"green":"red")} {family} {count}";
                var items=new ObservableCollection<object>();
                object Item(int i) => family switch {
                    "Live" => new LiveItemViewModel(), "Article" => new ArticleItemViewModel(), "User" => new UserItemViewModel(),
                    "Moment" => new MomentItemViewModel(), "Season" => new SeasonItemViewModel(), "Notify" => new NotifyMessageItemViewModel(),
                    _ => new VideoItemViewModel(new VideoInformation(new($"{family}-{i}","Fixture",null),null,bvId:$"{family} item {i:0000} local title"),VideoCardStyle.Recommend)
                };
                for(int i=0;i<count;i++) items.Add(Item(i));
                var layoutKey=family switch {"Live"=>"LiveLayout","Article"=>"ArticleLayout","User"=>"UserLayout","Moment"=>"MomentLayout","Season"=>"PgcLayout",_=>"VideoLayout"};
                var owner=new AppRepeater {ItemsSource=items,ItemTemplate=(DataTemplate)templates[family], Layout=family is "Stack" or "Notify"?new StackLayout {Spacing=8}:(VirtualizingLayout)Resources[layoutKey]};
                var commandCalls=0;
                var more=new CommunityToolkit.Mvvm.Input.RelayCommand(()=>commandCalls++);
                var view=new IncrementVerticalScrollControl {Element=owner,LoadMoreCommand=more};
                Grid.SetColumnSpan(view,2);Grid.SetRowSpan(view,2);
                slot.Children.Clear();slot.Children.Add(view);
                window.AppWindow.Resize(new(1400,900));
                await Task.Delay(1800);
                var sv=(ScrollView)view.FindName("View");
                var ir=(ItemsRepeater)owner.FindName("Repeater");
                Require(sv.ViewportHeight>0 && double.IsFinite(sv.ViewportHeight),"FiniteViewport");
                Require(ir.VerticalCacheLength==(green?2:20),"AppliedCache");
                int Realized()=>Enumerable.Range(0,items.Count).Count(i=>ir.TryGetElement(i)!=null);
                // Let the framework fill its cache before timing. A fixed short sleep can
                // otherwise mix the package's incremental cache growth into the resize sample.
                var stable=0;var previous=-1;
                for(int attempt=0;attempt<200 && stable<8;attempt++) {
                    var current=Realized();
                    var ready=Descendants(owner).OfType<ImageExBase>().Where(c=>c.Source!=null).All(HasImage)
                        && ResizeProbe.Active==0;
                    stable=current==previous && ready?stable+1:0;previous=current;
                    await Task.Delay(100);
                }
                Require(stable>=8,"InitialCacheAndImagesSettled");
                var realized=Realized();
                var mutations=0;System.Collections.Specialized.NotifyCollectionChangedEventHandler changed=(_,_)=>mutations++;
                items.CollectionChanged+=changed;
                var beats=new List<double>();var resize=new List<double>();
                var timer=Dependencies.Queue.CreateTimer();timer.Interval=TimeSpan.FromMilliseconds(16);
                var sw=Stopwatch.StartNew();var last=sw.Elapsed.TotalMilliseconds;
                timer.Tick+=(_,_)=>{var now=sw.Elapsed.TotalMilliseconds;beats.Add(now-last);last=now;};
                ListProbe.Measures=ListProbe.Arranges=ListProbe.Draws=0;ResizeProbe.Reset();
                var layouts=0;EventHandler<object> onLayout=(_,_)=>layouts++;
                owner.LayoutUpdated+=onLayout;timer.Start();
                var hwnd=Microsoft.UI.Win32Interop.GetWindowFromWindowId(window.AppWindow.Id);
                var allocated=await Task.Run(async()=>{
                    var before=GC.GetTotalAllocatedBytes();
                    for(int i=0;i<120;i++) {
                        var watch=Stopwatch.StartNew();
                        if(!SetWindowPos(hwnd,IntPtr.Zero,0,0,1400+(i<60?i:120-i)*3,900,0x16))throw new Exception("SetWindowPos");
                        resize.Add(watch.Elapsed.TotalMilliseconds);await Task.Delay(16);
                    }
                    return GC.GetTotalAllocatedBytes()-before;
                });
                await Task.Delay(800);timer.Stop();owner.LayoutUpdated-=onLayout;
                items.CollectionChanged-=changed;Require(mutations==0 && items.Count==count,"ResizePreservesCollection");
                var measures=ListProbe.Measures+ResizeProbe.Measures;var arranges=ListProbe.Arranges+ResizeProbe.Arranges;
                var draws=ListProbe.Draws;var decoders=ResizeProbe.Decoders;
                var scrollDraws=ListProbe.Draws;var peakRealized=realized;
                var scrollBeats=new List<double>();
                var scrollTimer=Dependencies.Queue.CreateTimer();scrollTimer.Interval=TimeSpan.FromMilliseconds(16);
                var scrollClock=Stopwatch.StartNew();var lastScrollBeat=scrollClock.Elapsed.TotalMilliseconds;
                scrollTimer.Tick+=(_,_)=>{var now=scrollClock.Elapsed.TotalMilliseconds;scrollBeats.Add(now-lastScrollBeat);lastScrollBeat=now;};
                scrollTimer.Start();
                // Both large jumps and continuous one-third viewport steps, including reverse direction.
                var positions=new List<double>{sv.ScrollableHeight*.5,sv.ScrollableHeight,0};
                for(int i=1;i<=12;i++)positions.Add(Math.Min(sv.ScrollableHeight,i*sv.ViewportHeight/3));
                for(int i=11;i>=0;i--)positions.Add(Math.Min(sv.ScrollableHeight,i*sv.ViewportHeight/3));
                var scrolling=new List<object>();
                foreach(var y in positions) {
                    var scrollLatency=await ScrollAndWait(sv,y);
                    await Task.Delay(100);
                    var visible=Enumerable.Range(0,items.Count).Select(i=>ir.TryGetElement(i)).OfType<FrameworkElement>().Where(e=>{
                        var p=e.TransformToVisual(sv).TransformPoint(new(0,0));return p.Y+e.ActualHeight>0&&p.Y<sv.ViewportHeight;
                    }).ToArray();
                    Require(visible.Length>0,"ScrollRealizedVisible");
                    peakRealized=Math.Max(peakRealized,Realized());
                    var pendingVisibleImages=visible.SelectMany(Descendants).OfType<ImageExBase>().Count(c=>c.Source!=null&&!HasImage(c));
                    scrolling.Add(new{requested=y,actual=sv.VerticalOffset,visible=visible.Length,realized=Realized(),pendingVisibleImages,scrollLatency});
                }
                await Task.Delay(1000);
                scrollTimer.Stop();
                var images=Descendants(owner).OfType<ImageExBase>().Where(c=>c.IsLoaded && c.Source!=null).ToArray();
                var missing=images.Count(c=>!HasImage(c));
                Require(missing==0,"ScrolledImagesPresent");
                var beforeMore=commandCalls;
                await ScrollAndWait(sv,sv.ScrollableHeight);await Task.Delay(600);
                Require(commandCalls>beforeMore,"ContinuousLoadingCommand");
                items.Add(Item(count));await Task.Delay(200);Require(items.Count==count+1,"AppendPreserved");items.RemoveAt(count);
                await ScrollAndWait(sv,0);await Task.Delay(700);
                slot.Children.Clear();await Task.Delay(100);slot.Children.Add(view);await Task.Delay(1000);
                Require(ir.VerticalCacheLength==(green?2:20),"RemountCache");
                Require(Descendants(owner).OfType<ImageExBase>().Where(c=>c.IsLoaded && c.Source!=null).All(HasImage),"RemountImagesPresent");
                var cards=Descendants(owner).OfType<FrameworkElement>().Select(c=>(card:c,command:c switch {
                    VideoCardControl v => v.ViewModel.PlayCommand as System.Windows.Input.ICommand,
                    LiveCardControl v => v.ViewModel.PlayCommand,
                    ArticleCardControl v => v.ViewModel.OpenReaderCommand,
                    UserCardControl v => v.ViewModel.ShowUserSpaceCommand,
                    MomentCardControl v => v.ViewModel.ActivateCommand,
                    SeasonCardControl v => v.ViewModel.PlayCommand,
                    BiliCopilot.UI.Controls.Message.NotifyMessageCardControl v => v.ViewModel.ActiveCommand,
                    _ => null
                })).Where(x=>x.command!=null).ToArray();
                Require(cards.Length>0 && cards.All(x=>ReferenceEquals(Descendants(x.card).OfType<Microsoft.UI.Xaml.Controls.Primitives.ButtonBase>().FirstOrDefault(b=>b.Name=="RootCard")?.Command,x.command)),"RemountPrimaryNavigationBinding");
                await Capture(sv,Path.Combine(output,$"matrix-{family}-{count}.png"));
                double P95(List<double> a)=>a.Order().ElementAt((int)((a.Count-1)*.95));
                rows.Add(new{family,count,green,dpi=sv.XamlRoot.RasterizationScale,cache=ir.VerticalCacheLength,realized,peakRealized,layouts,measures,arranges,draws,decoders,ResizeProbe.NativePeak,ResizeProbe.Active,allocated,heartbeatMax=beats.Max(),heartbeatP95=P95(beats),resizeP95=P95(resize),resizeMax=resize.Max(),scrollDraws=ListProbe.Draws-scrollDraws,scrollHeartbeatMax=scrollBeats.Max(),scrollHeartbeatP95=P95(scrollBeats),missing,scrolling,resize,beats,scrollBeats});
                File.WriteAllText(Path.Combine(output,"matrix-results.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
                void Require(bool condition,string name) { checks.Add($"{family}/{count}/{name}/{(condition?"PASS":"FAIL")}"); }
            }
            if(extra) await RunCoverChecks(output);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"matrix-error.txt"),e.ToString());status.Text=e.Message;}
        File.WriteAllText(Path.Combine(output,"matrix-checks.json"),JsonSerializer.Serialize(checks));
        File.WriteAllText(Path.Combine(output,"matrix-finished.txt"),DateTimeOffset.UtcNow.ToString("O"));
    }
    static bool HasImage(ImageExBase c)=>Descendants(c).OfType<Panel>().Any(p=>p.Background is ImageBrush {ImageSource:not null});
    static async Task<double> ScrollAndWait(ScrollView view,double target)
    {
        var completion=new TaskCompletionSource();var id=-2;
        void Completed(ScrollView sender,ScrollingScrollCompletedEventArgs args){if(args.CorrelationId==id)completion.TrySetResult();}
        view.ScrollCompleted+=Completed;
        var clock=Stopwatch.StartNew();
        try {
            id=view.ScrollTo(0,target,new ScrollingScrollOptions(ScrollingAnimationMode.Disabled));
            if(id!=-1) await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
            return clock.Elapsed.TotalMilliseconds;
        }
        finally {view.ScrollCompleted-=Completed;}
    }
    static async Task Capture(FrameworkElement element,string path)
    {
        var bitmap=new RenderTargetBitmap();await bitmap.RenderAsync(element);
        var pixels=await bitmap.GetPixelsAsync();
        using var stream=File.OpenWrite(path);using var random=stream.AsRandomAccessStream();
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,random);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(pixels));
        await encoder.FlushAsync();
    }
}
