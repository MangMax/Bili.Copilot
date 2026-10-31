using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Toolkits;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using System.Reflection;
using System.Text.Json;
namespace Review;
public partial class App:Application
{
    Window window, second; StackPanel panel=new(); bool running;
    readonly List<object> checks=new();
    string Output=>Windows.Storage.ApplicationData.Current.LocalFolder.Path;
    static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static Image Image(RecommendationCoverImage c)=>(Image)((Grid)c.Content).Children[0];
    static SemaphoreSlim Slots=>(SemaphoreSlim)typeof(RecommendationCoverImage).GetField("Decodes",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
    static void Require(bool b,string why){if(!b)throw new Exception(why);}
    static async Task Until(Func<bool> f){for(int i=0;i<200&&!f();i++)await Task.Delay(25);Require(f(),"Condition timed out");}
    Uri Source=>new(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Cover.png"));
    RecommendationCoverImage New()=>new(){Width=240,Height=135,Source=Source};
    async Task Check(string name,Func<Task> action){try{await action();checks.Add(new{name,status="PASS"});}catch(Exception e){checks.Add(new{name,status="FAIL",error=e.ToString()});}File.WriteAllText(Path.Combine(Output,"checks.json"),JsonSerializer.Serialize(checks));}
    public App(){InitializeComponent();UnhandledException+=(_,e)=>File.AppendAllText(Path.Combine(Output,"unhandled.txt"),e.Exception.ToString());}
    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        window=new(){Title="Owned resize independent review"};
        var run=new Button{Content="Run"};AutomationProperties.SetAutomationId(run,"Run");run.Click+=async(_,_)=>await Run();
        var close=new Button{Content="Close"};AutomationProperties.SetAutomationId(close,"Close");close.Click+=(_,_)=>{second?.Close();second=null;window.Close();};
        panel.Children.Add(run);panel.Children.Add(close);window.Content=panel;window.Activate();
    }
    async Task Run()
    {
        if(running)return;
        running=true;
        await Check("InitialLoadSameSizeNotificationsAndFinalPixels",async()=>{
            var c=New();panel.Children.Add(c);await Until(()=>Image(c).Source is not null);
            c.Width=300;await Task.Delay(40);
            var timer=Field(c,"_resizeTimer");Require(timer is not null,"Real dispatcher timer missing");
            var schedule=typeof(RecommendationCoverImage).GetMethod("ScheduleResize",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int i=0;i<20;i++){schedule.Invoke(c,null);await Task.Delay(30);}
            var expected=CoverDecodeSize.Calculate(1600,900,c.ActualWidth,c.ActualHeight,c.XamlRoot.RasterizationScale);
            var surface=(CanvasImageSource)Image(c).Source;
            Require(surface.SizeInPixels.Width==expected.Item1&&surface.SizeInPixels.Height==expected.Item2,"Repeated same-size notification postponed decode");
            panel.Children.Remove(c);
        });
        await Check("SourceReplacementStopsTimerAndRemountUsesCurrentSource",async()=>{
            var c=New();panel.Children.Add(c);await Until(()=>Image(c).Source is not null);
            c.Width=350;await Task.Delay(30);
            c.Source=new(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Logo.png"));
            var timer=(Microsoft.UI.Dispatching.DispatcherQueueTimer)Field(c,"_resizeTimer");Require(!timer.IsRunning,"Source change left timer running");
            await Until(()=>Image(c).Source is not null);var current=Image(c).Source;
            await Task.Delay(400);Require(ReferenceEquals(current,Image(c).Source),"Old timer replaced new surface");
            panel.Children.Remove(c);await Task.Delay(50);Require(Image(c).Source is null,"Unload retained surface");
            panel.Children.Add(c);await Until(()=>Image(c).Source is not null);panel.Children.Remove(c);
        });
        await Check("QueuedUnloadDoesNotLeakOrOverReleaseSlots",async()=>{
            await Until(()=>Slots.CurrentCount==2);await Slots.WaitAsync();await Slots.WaitAsync();
            var c=New();try{panel.Children.Add(c);await Until(()=>c.IsLoaded);await Task.Delay(100);panel.Children.Remove(c);await Task.Delay(100);Require(Slots.CurrentCount==0,"Cancelled waiter released unowned slot");}finally{Slots.Release(2);}
            await Until(()=>Slots.CurrentCount==2);Require(Image(c).Source is null,"Unloaded waiter published");
        });
        await Check("InvalidWicImageReleasesSlotAndNewSourceRecovers",async()=>{
            var path=Path.Combine(Output,"invalid.png");File.WriteAllText(path,"invalid image");
            var c=New();c.Source=new(path);panel.Children.Add(c);await Until(()=>c.IsLoaded);await Task.Delay(400);
            Require(Slots.CurrentCount==2&&Image(c).Source is null,"WIC exception leaked slot");
            c.Source=Source;await Until(()=>Image(c).Source is not null);panel.Children.Remove(c);
        });
        await Check("TwoWindowsDrainSharedBudgetAndClosedWindowReleases",async()=>{
            var other=new StackPanel();second=new(){Title="Owned resize independent review second",Content=other};second.Activate();
            var covers=Enumerable.Range(0,24).Select(_=>New()).ToArray();
            for(int i=0;i<covers.Length;i++)(i%2==0?panel:other).Children.Add(covers[i]);
            await Until(()=>covers.All(c=>Image(c).Source is not null));Require(Slots.CurrentCount==2,"Completed windows hold slots");
            foreach(var c in covers){c.Width=310;}
            second.Close();second=null;await Task.Delay(700);
            await Until(()=>Slots.CurrentCount==2);foreach(var c in covers.Where((_,i)=>i%2==0))panel.Children.Remove(c);
        });
        await Check("RapidNativeUnloadAndSurfaceRecovery",async()=>{
            for(int i=0;i<40;i++){var c=New();panel.Children.Add(c);await Until(()=>c.IsLoaded);await Task.Delay(1);panel.Children.Remove(c);}
            await Until(()=>Slots.CurrentCount==2);
            var final=New();panel.Children.Add(final);await Until(()=>Image(final).Source is not null);
            var old=Image(final).Source;
            typeof(RecommendationCoverImage).GetMethod("OnSurfaceContentsLost",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(final,new object[]{null,new object()});
            await Until(()=>Image(final).Source is not null&&!ReferenceEquals(old,Image(final).Source));
            Require(Slots.CurrentCount==2,"Recovery retained slot");
        });
        File.WriteAllText(Path.Combine(Output,"finished.txt"),DateTimeOffset.UtcNow.ToString("O"));
    }
}
