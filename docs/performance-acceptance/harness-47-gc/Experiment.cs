using BiliCopilot.UI.Controls.Markdown;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Acceptance;
public partial class App : Application
{
    Window window;
    StackPanel host;
    TextBlock status;
    MarkdownTextBlock shown;
    readonly List<object> rows = new(); // scalar snapshots only
    readonly List<(WeakReference control, WeakReference document, WeakReference block)> weak = new();
    readonly string output = Path.Combine(AppContext.BaseDirectory, "experiment");
    const string Fixture = "# GC47 fixed document\n\nALPHA text with [offline hyperlink](https://fixture.invalid/link).\n\n![offline image](https://fixture.invalid/image.png)\n\nFINAL-OMEGA";
    int step;
    public App() { InitializeComponent(); Directory.CreateDirectory(output); }
    protected override void OnLaunched(LaunchActivatedEventArgs args) {
        window = new Window { Title = "GC47 controlled comparison" };
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(900,700));
        window.AppWindow.Move(new Windows.Graphics.PointInt32(50,50));
        var root = new StackPanel { Padding = new Thickness(20), Spacing = 12 };
        var next = new Button { Content = "Next" }; AutomationProperties.SetAutomationId(next,"Next");
        var close = new Button { Content = "Close" }; AutomationProperties.SetAutomationId(close,"Close"); close.Click += (_,_) => window.Close();
        status = new TextBlock { Text = "Idle" }; AutomationProperties.SetAutomationId(status,"Status");
        host = new StackPanel { Width = 700, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(next); root.Children.Add(close); root.Children.Add(status); root.Children.Add(host);
        next.Click += async (_,_) => {
            next.IsEnabled=false; status.Text="Running";
            try {
                if(step==0) { shown=New(); host.Children.Add(shown); await Ready(shown); Check("Initial",shown); }
                if(step==1) { shown.Text=""; await Flush(); Require(Doc(shown).Blocks.Count==0,"EmptyClears"); }
                if(step==2) {
                    shown.Text=Fixture; await Ready(shown); Check("Reassign",shown);
                    for(int i=0;i<3;i++) { host.Children.Remove(shown); await Flush(); Require(!shown.IsLoaded && Doc(shown).Blocks.Count==0,"Unload"+i); host.Children.Add(shown); await Ready(shown); Check("SameInstanceUnchanged"+i,shown); }
                    shown.Text=""; host.Children.Remove(shown); await Flush();
                    var loaded=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    RoutedEventHandler onLoaded=(_,_)=>loaded.TrySetResult();shown.Loaded+=onLoaded;
                    try {host.Children.Add(shown);await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));await Flush();}
                    finally {shown.Loaded-=onLoaded;}
                    rows.Add(new{boundary="EmptyRemount",shown.IsLoaded,blocks=Doc(shown).Blocks.Count});
                    Require(shown.IsLoaded && Doc(shown).Blocks.Count==0,"EmptyRemount");
                    shown.Text=Fixture; await Ready(shown); Check("AfterEmptyRemount",shown);
                }
                if(step==3) {
                    host.Children.Clear(); shown=null; await Flush();
                    for(int i=0;i<5;i++) await Cycle(false);
                    Sample("measure-start");
                    for(int i=0;i<50;i++) await Cycle(true);
                    Sample("measure-end");
                    // Fixed idle observation: no artificial allocation pressure or forced GC.
                    var clock=Stopwatch.StartNew();
                    for(int i=1;i<=10;i++) { await Task.Delay(Math.Max(1,(int)(i*1000-clock.ElapsedMilliseconds))); Sample("observe-"+i); }
                }
                step++; Save(); status.Text="Ready "+step;
            } catch(Exception e) { rows.Add(new {failure=e.ToString(),utc=DateTime.UtcNow}); Save(); status.Text="FAIL "+e.Message; }
            finally { next.IsEnabled=true; }
        };
        window.Content=root; window.Activate();
        File.WriteAllText(Path.Combine(output,"fixture.md"),Fixture);
    }
    void Save()=>File.WriteAllText(Path.Combine(output,"records.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    void Require(bool ok,string name) { rows.Add(new{name,pass=ok,utc=DateTime.UtcNow}); if(!ok)throw new Exception(name); }
    MarkdownTextBlock New()=>new(){Config=new MarkdownConfig{ImageProvider=new Offline()},Text=Fixture};
    static IEnumerable<DependencyObject> Walk(DependencyObject p) { for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);yield return c;foreach(var d in Walk(c))yield return d;} }
    static RichTextBlock Doc(MarkdownTextBlock c)=>Walk(c).OfType<RichTextBlock>().Single();
    static IEnumerable<Inline> Inlines(IEnumerable<Inline> xs) { foreach(var x in xs){yield return x;if(x is Span s)foreach(var y in Inlines(s.Inlines))yield return y;} }
    void Check(string name,MarkdownTextBlock c) {
        var d=Doc(c);var inline=d.Blocks.OfType<Paragraph>().SelectMany(p=>Inlines(p.Inlines)).ToArray();
        var text=string.Concat(inline.OfType<Run>().Select(r=>r.Text));
        Require(c.IsLoaded && c.Text==Fixture && d.Blocks.Count==4 && text.Contains("ALPHA") && text.Contains("FINAL-OMEGA") && inline.OfType<Hyperlink>().Any(h=>h.NavigateUri.AbsoluteUri=="https://fixture.invalid/link") && ImageReady(c),name);
    }
    static bool ImageReady(MarkdownTextBlock c) {var images=Walk(c).OfType<Image>().ToArray();return images.Length==1 && images.All(i=>i.IsLoaded && i.ActualWidth==96 && i.ActualHeight==96 && i.Source is BitmapImage{PixelWidth:150,PixelHeight:150});}
    async Task Flush() {var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,()=>t.TrySetResult());await t.Task.WaitAsync(TimeSpan.FromSeconds(5));host.UpdateLayout();}
    async Task Frames() {var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int n=0;EventHandler<object> h=(_,_)=>{if(++n==2)t.TrySetResult();};CompositionTarget.Rendering+=h;try{await t.Task.WaitAsync(TimeSpan.FromSeconds(5));}finally{CompositionTarget.Rendering-=h;}}
    async Task Ready(MarkdownTextBlock c) {
        var clock=Stopwatch.StartNew();
        while(clock.Elapsed.TotalSeconds<10) {
        var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<object> h=(_,_)=>{if(ImageReady(c))t.TrySetResult();};host.LayoutUpdated+=h;
        try{await Flush();if(ImageReady(c))t.TrySetResult();await t.Task.WaitAsync(TimeSpan.FromSeconds(10)-clock.Elapsed);}finally{host.LayoutUpdated-=h;}
        await Frames();if(ImageReady(c))return;
        rows.Add(new{boundary="ReadinessRecheck",elapsedMs=clock.Elapsed.TotalMilliseconds,utc=DateTime.UtcNow});
        }
        throw new Exception("Current image did not stabilize within 10 seconds");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    void Track(MarkdownTextBlock c) {var d=Doc(c);weak.Add((new(c),new(d),new(d.Blocks[0])));}
    async Task Cycle(bool measured) {
        var c=New();host.Children.Add(c);await Ready(c);Check(measured?"MeasuredRender":"WarmupRender",c);
        if(measured)Track(c);
        // Same semantic operations in both arms: one empty Text clean and one Unloaded clean.
        var start=Stopwatch.GetTimestamp();c.Text="";var emptyMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Require(Doc(c).Blocks.Count==0,"CycleEmpty");host.Children.Remove(c);await Flush();Require(!c.IsLoaded && Doc(c).Blocks.Count==0,"CycleUnload");
        c=null;await Frames();await Task.Delay(100);
        if(measured) rows.Add(new{cycle=weak.Count,emptyMs,utc=DateTime.UtcNow});
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    void Sample(string name) {
        using var p=Process.GetCurrentProcess();var g=GC.GetGCMemoryInfo();
        rows.Add(new{name,utc=DateTime.UtcNow,qpc=Stopwatch.GetTimestamp(),qpcFrequency=Stopwatch.Frequency,gen=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray(),allocated=GC.GetTotalAllocatedBytes(false),managed=GC.GetTotalMemory(false),privateBytes=p.PrivateMemorySize64,workingSet=p.WorkingSet64,total=weak.Count,controlsAlive=weak.Count(x=>x.control.IsAlive),documentsAlive=weak.Count(x=>x.document.IsAlive),blocksAlive=weak.Count(x=>x.block.IsAlive),lastGcIndex=g.Index,lastGcGeneration=g.Generation,lastGcPauseMs=g.PauseDurations.ToArray().Select(t=>t.TotalMilliseconds).ToArray(),runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,serverGC=System.Runtime.GCSettings.IsServerGC});
    }
    sealed class Offline:IImageProvider {
        public bool ShouldUseThisProvider(string url)=>url=="https://fixture.invalid/image.png";
        public async Task<Image> GetImage(string url) {
            var b=new BitmapImage();using var f=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Assets","Logo.png"));
            await b.SetSourceAsync(f.AsRandomAccessStream());
            // Awaited stream decoding before publication; no Image-retaining event dictionary.
            return new Image{Width=96,Height=96,Source=b};
        }
    }
}
