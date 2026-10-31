// Experiment-only service/launch injection. All production PlayerVM partials and controls remain intact.
#pragma warning disable VSTHRD003 // Experimental async gates and read-only retirement observer; never synchronously wait.
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using BiliCopilot.UI.Controls.Core;
using BiliCopilot.UI.Controls.Danmaku;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Components;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Models.Danmaku;
using RichasyKernel;
namespace BiliCopilot.UI;
internal static class OfflineAcceptance
{
    internal const string Root = @"C:\Users\hp\.t3\worktrees\Bili.Copilot\t3-dc85fdad\artifacts\speed-close\full";
    static readonly BiliImage Cover = new(new Uri("ms-appx:///Assets/Logo/StoreLogo.png"));
    internal static readonly UserProfile User = new("224", "Synthetic offline account", Cover);
    internal static readonly VideoInformation Video = new(new("224", "Offline full player 224", Cover),new(User),60,"BVoffline224",DateTimeOffset.Now,communityInformation:new("224"));
    internal static VideoPlayerView View = new(Video,new("224"),[new(new("225","Offline part",Cover),1,60)],tags:[],operation:new("224",false,false,false));
    static PlayerViewModel? vm;
    static DispatcherQueueTimer? observer;
    static Window? control;
    internal static TaskCompletionSource? ServiceGate;
    internal static bool HoldNextDetail;
    static int generation;
    static bool closeOnSeeking;
    static readonly HashSet<object> owners=new();
    static readonly HashSet<object> hooked=new();
    static object? Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(o);
    static IEnumerable<DependencyObject> Desc(DependencyObject o) { yield return o;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var c in Desc(VisualTreeHelper.GetChild(o,i)))yield return c; }
    static void ObserveVisual()
    {
        if(vm?.Window?.IsClosed!=false)return;
        var nodes=Desc(vm.Window.XamlRoot.Content).ToArray();
        foreach(var fe in nodes.OfType<FrameworkElement>()) {
            if(!string.IsNullOrEmpty(fe.Name))Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(fe,fe.Name);
            if(fe is PlayerInteractivePanel p&&hooked.Add(p)) {
                foreach(var (ev,name) in new[]{(UIElement.PointerPressedEvent,"pressed"),(UIElement.PointerReleasedEvent,"released"),(UIElement.PointerCanceledEvent,"canceled"),(UIElement.PointerCaptureLostEvent,"lost"),(UIElement.PointerMovedEvent,"moved")}) {
                    p.AddHandler(ev,new PointerEventHandler((_,e)=>Log(new{kind="pointer",ev=name,id=e.Pointer.PointerId,owner=Field(p,"_gesturePointerId"),captures=p.PointerCaptures?.Select(c=>c.PointerId).ToArray(),preview=vm.IsPreviewProgressChanging,position=vm.Player?.Position})),true);
                }
            }
        }
        var panel=nodes.OfType<VideoDanmakuPanel>().FirstOrDefault();
        if(panel==null){Log(new{kind="visual",panel=false});return;}
        var renderer=Field(panel,"_danmakuRenderer");
        if(renderer==null){Log(new{kind="visual",panel=true,renderer=false});return;}
        var owner=Field(renderer,"_owner");var surface=owner==null?null:Field(owner,"surface");
        if(owner!=null&&owners.Add(owner)) {
            var retirement=(Task)owner.GetType().GetProperty("Retirement",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
            var ownerId=owners.Count;
            Log(new{kind="owner-seen",ownerId});
            _=ReportRetirement(retirement,ownerId);
        }
        var native=surface==null?null:Field(surface,"renderer");
        if(native==null)return;
        var total=(TimeSpan)Field(surface!,"total")!;
        var rows=((Array)Field(native,"_renderLayerList")!).Cast<object>().SelectMany(l=>((System.Collections.IEnumerable)Field(l,"RenderList")!).Cast<object>()).Select(i=>new {text=((Richasy.Danmaku.Models.DanmakuItem)Field(i,"Item")!).Text,x=Field(i,"X"),y=Field(i,"StartY"),width=Field(i,"Width"),height=Field(i,"Height"),age=(total-(TimeSpan)Field(i,"FirstRenderTime")!).TotalMilliseconds}).ToArray();
        Log(new{kind="visual",panel=true,renderer=true,paused=Field(renderer,"_isPaused"),total=total.TotalMilliseconds,rows});
    }
    static async Task ReportRetirement(Task retirement,int ownerId) {
        try { await retirement;Log(new{kind="owner-retired",ownerId}); }
        catch(Exception e) { Log(new{kind="retirement-error",ownerId,error=e.ToString()}); }
    }
    internal static void Log(object data) => File.AppendAllText(Path.Combine(Root,"events.jsonl"),JsonSerializer.Serialize(new { at=DateTimeOffset.UtcNow,data })+"\n");
    public static IKernelBuilder AddOfflineAcceptance(this IKernelBuilder b)
    {
        b.Services.AddSingleton(DispatchProxy.Create<IPlayerService, OfflineProxy>());
        b.Services.AddSingleton(DispatchProxy.Create<ISubtitleService, OfflineProxy>());
        b.Services.AddSingleton(DispatchProxy.Create<IDanmakuService, OfflineProxy>());
        b.Services.AddSingleton(DispatchProxy.Create<IBiliTokenResolver, OfflineProxy>());
        b.Services.AddSingleton(DispatchProxy.Create<IBiliCookiesResolver, OfflineProxy>());
        return b;
    }
    internal static async void Start(App app)
    {
        try
        {
            app.UnhandledException += (_,e)=>Log(new {kind="unhandled",error=e.Exception.ToString()});
            GlobalDependencies.Initialize();
            SettingsToolkit.WriteLocalSetting(SettingNames.HideMainWindowOnPlay,false);
            SettingsToolkit.WriteLocalSetting(SettingNames.ShouldReportProgress,false);
            SettingsToolkit.WriteLocalSetting(SettingNames.SponsorBlockEnabled,false);
            SettingsToolkit.WriteLocalSetting(SettingNames.DanmakuRenderer,DanmakuRendererType.DirectX);
            SettingsToolkit.WriteLocalSetting(SettingNames.StepBackwardSecond,1d);
            SettingsToolkit.WriteLocalSetting(SettingNames.StepForwardSecond,1d);
            SettingsToolkit.WriteLocalSetting(SettingNames.CustomLibmpvPath,Path.Combine(Root,"libmpv-2.dll"));
            GlobalDependencies.Kernel.GetRequiredService<AccountViewModel>().MyProfile=new(User);
            app.InitializeTrayLifecycle();
            control=new Window {Title="Offline acceptance 224 controller"};
            var buttons=new StackPanel();
            void Button(string id,Func<Task> action) {
                var b=new Button {Content=id};Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(b,id);
                b.Click+=async (_,_)=>{try{await action();}catch(Exception ex){Log(new{kind="action-error",id,error=ex.ToString()});}};buttons.Children.Add(b);
            }
            Button("OpenFull",Open);
            Button("CloseFull",async()=>{if(vm!=null){var old=vm;Log(new{kind="close-enter",pendingInitialization=!(Field(old,"_initializationTask") as Task)!.IsCompleted,old.IsConnecting});await old.CloseAsync();Log(new{kind="closed",disposed=old.Client?.IsDisposed,remaining=GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().Players.Count});}});
            Button("NormalSpeed",async()=>{await vm!.IncreaseSpeedCommand.ExecuteAsync(null);Log(new{kind="normal-speed-command",speed=vm.Player?.PlaybackRate});});
            Button("ShowControls",()=>{vm!.IsControlsVisible=true;return Task.CompletedTask;});
            Button("SpeedClose",async()=>{var old=vm!;var tasks=Enumerable.Range(0,20).Select(i=>old.ChangeSpeedAsync(i%2==0?1.5:2)).ToArray();Log(new{kind="speed-close-enter",pending=tasks.Count(t=>!t.IsCompleted)});await old.CloseAsync();await Task.WhenAll(tasks);Log(new{kind="speed-close-done",disposed=old.Client?.IsDisposed,completed=tasks.All(t=>t.IsCompleted)});});
            Button("OpenHeld",()=>{HoldNextDetail=true;return Open();});
            Button("ReleaseService",()=>{ServiceGate?.TrySetResult();Log(new{kind="service-release"});return Task.CompletedTask;});
            Button("SwitchMedia",async()=>{generation++;await vm!.InitializeAsync(new MediaSnapshot(new VideoInformation(new((224+generation).ToString(),"Offline switched "+generation,Cover),new(User),32,"BVoffline"+generation,DateTimeOffset.Now,communityInformation:new((224+generation).ToString())),true){StartPosition=19});Log(new{kind="switched",generation});});
            Button("ArmSwitch",()=>{Schedule(async()=>{
                generation++;var id=(500+generation).ToString();
                var next=new VideoInformation(new(id,"Offline switched "+id,Cover),new(User),32,"BVoffline"+id,DateTimeOffset.Now,communityInformation:new(id));
                View=new(next,new(id),[new(new(id+"1","Offline switched part",Cover),1,32)],tags:[],operation:new(id,false,false,false));
                Log(new{kind="scheduled-switch-enter",mediaId=id});await vm!.InitializeAsync(new MediaSnapshot(next,true){StartPosition=19});Log(new{kind="scheduled-switch-done",mediaId=id});
            });return Task.CompletedTask;});
            Button("ArmClose",()=>{Schedule(async()=>{Log(new{kind="scheduled-close-enter"});await vm!.CloseAsync();Log(new{kind="scheduled-close-done",disposed=vm.Client?.IsDisposed});});return Task.CompletedTask;});
            Button("ArmSeekClose",()=>{closeOnSeeking=true;return Task.CompletedTask;});
            Button("UnloadDanmaku",()=>{vm!.IsDanmakuControlVisible=false;return Task.CompletedTask;});
            Button("LoadDanmaku",()=>{vm!.IsDanmakuControlVisible=true;return Task.CompletedTask;});
            Button("ExitOwned",async()=>{if(vm!=null)await vm.CloseAsync();observer?.Stop();app.ReleaseTrayResources();control.Close();app.Exit();});
            control.Content=buttons;control.Activate();
            observer=DispatcherQueue.GetForCurrentThread().CreateTimer();observer.Interval=TimeSpan.FromMilliseconds(250);
            observer.Tick+=(_,_)=>{try{Log(new{kind="state",position=vm?.Player?.Position,state=vm?.Player?.PlaybackState.ToString(),speed=vm?.Player?.PlaybackRate,connecting=vm?.IsConnecting,error=vm?.ErrorMessage,danmakuPosition=vm?.Danmaku.Position,danmakuPaused=vm?.Danmaku.IsPaused,xload=vm?.IsDanmakuControlVisible,closed=vm?.Window?.IsClosed});ObserveVisual();}catch(Exception ex){Log(new{kind="observe-error",error=ex.Message});}};
            observer.Start();await Open();
        }
        catch(Exception ex){Log(new{kind="startup-error",error=ex.ToString()});}
    }
    static async Task Open()
    {
        View=new(Video,new("224"),[new(new("225","Offline part",Cover),1,60)],tags:[],operation:new("224",false,false,false));
        vm=GlobalDependencies.Kernel.GetRequiredService<PlayerViewModel>();
        GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().Players.Add(vm);
        vm.Danmaku.SeekRequested+=(_,position)=>Log(new{kind="seek-publication",position});
        await vm.InitializeAsync(new MediaSnapshot(Video,true){StartPosition=19});
        if(vm.Player is System.ComponentModel.INotifyPropertyChanged changes) changes.PropertyChanged+=async(_,_)=>{
            if(closeOnSeeking&&vm.Player.PlaybackState.ToString()=="Seeking") {
                closeOnSeeking=false;Log(new{kind="seeking-close-enter",position=vm.Player.Position});
                await vm.CloseAsync();Log(new{kind="seeking-close-done",disposed=vm.Client?.IsDisposed});
            }
        };
        Log(new{kind="initialized",client=vm.Client!=null,player=vm.Player!=null,connector=vm.Connector?.GetType().FullName,vm.ErrorMessage});
    }
    static void Schedule(Func<Task> action) {
        var timer=DispatcherQueue.GetForCurrentThread().CreateTimer();timer.Interval=TimeSpan.FromSeconds(3);timer.IsRepeating=false;
        timer.Tick+=async(_,_)=>{timer.Stop();try{await action();}catch(Exception e){Log(new{kind="scheduled-error",error=e.ToString()});}};
        timer.Start();Log(new{kind="scheduled",delaySeconds=3});
    }
}
public class OfflineProxy:DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)
    {
        var m=targetMethod!;OfflineAcceptance.Log(new {kind="service",method=m.Name});
        object? value=m.Name switch {
            "GetToken"=>new BiliToken{UserId=224},
            "GetCookieString"=>string.Empty,
            "GetVideoPageDetailAsync"=>OfflineAcceptance.View,
            "GetVideoPlayDetailAsync"=>new DashMediaInformation([new("80",Path.Combine(OfflineAcceptance.Root,"fixture-media.mkv"),1000000,"video/mp4","avc1",960,540,"","")],null,[new(80,"Offline",false,false)],60),
            "GetSegmentDanmakusWithGrpcAsync"=>(IReadOnlyList<DanmakuInformation>)new DanmakuInformation[]{new("a","Full source top A",5,19,0xffffff,25),new("b","Full source top B",5,19,0xffffff,25)},
            _=>null
        };
        var type=m.ReturnType;
        if(type==typeof(void))return null;
        if(type==typeof(Task))return Task.CompletedTask;
        if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(Task<>)) {
            var t=type.GenericTypeArguments[0];value??=Empty(t);
            if(m.Name=="GetVideoPageDetailAsync"&&OfflineAcceptance.HoldNextDetail) {
                OfflineAcceptance.HoldNextDetail=false;
                OfflineAcceptance.ServiceGate=new(TaskCreationOptions.RunContinuationsAsynchronously);
                OfflineAcceptance.Log(new{kind="service-held",method=m.Name});
                return typeof(OfflineProxy).GetMethod(nameof(Held),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(t).Invoke(null,[value,OfflineAcceptance.ServiceGate.Task]);
            }
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(t).Invoke(null,[value]);
        }
        return value??Empty(type);
    }
    static async Task<T> Held<T>(T value,Task gate) { await gate;return value; }
    static object? Empty(Type t) {
        if(t==typeof(string))return string.Empty;
        if(t.IsGenericType&&typeof(System.Collections.IEnumerable).IsAssignableFrom(t))return Activator.CreateInstance(typeof(List<>).MakeGenericType(t.GenericTypeArguments[0]));
        return t.IsValueType?Activator.CreateInstance(t):null;
    }
}
