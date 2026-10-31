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
    internal const string Root = @"C:\Users\hp\.t3\worktrees\Bili.Copilot\t3-6273bfc4\artifacts\remount-6273\full";
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
        var master=Field(panel,"_danmakuMaster");
        var native=master==null?null:Field(master,"_render");
        if(native==null){Log(new{kind="visual",panel=true,renderer=false});return;}
        var rows=new List<object>();
        if(Field(native,"_renderLayerList") is Array layers)foreach(var layer in layers){
            if(Field(layer!,"RenderList") is System.Collections.IList list)lock(list)foreach(var item in list)rows.Add(new{text=Field(item!,"Text"),x=Field(item!,"X"),y=Field(item!,"StartY"),drawable=Field(item!,"IsFirstRenderTimeSet"),first=Field(item!,"FirstRenderTime")});
        }
        Log(new{kind="visual",panel=true,renderer=true,master=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(master!),paused=Field(native,"_isPaused"),stopped=Field(native,"_isStopped"),time=Field(master!,"_lastTimeMs"),rows});
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
            SettingsToolkit.WriteLocalSetting(SettingNames.DanmakuRenderer,DanmakuRendererType.Win2D);
            SettingsToolkit.WriteLocalSetting(SettingNames.StepBackwardSecond,1d);
            SettingsToolkit.WriteLocalSetting(SettingNames.StepForwardSecond,1d);
            SettingsToolkit.WriteLocalSetting(SettingNames.CustomLibmpvPath,Path.Combine(Root,"libmpv-2.dll"));
            GlobalDependencies.Kernel.GetRequiredService<AccountViewModel>().MyProfile=new(User);
            app.InitializeTrayLifecycle();
            control=new Window {Title="Seek pause 2bd22 controller"};
            var buttons=new StackPanel();
            void Button(string id,Func<Task> action) {
                var b=new Button {Content=id};Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(b,id);
                b.Click+=async (_,_)=>{try{await action();}catch(Exception ex){Log(new{kind="action-error",id,error=ex.ToString()});}};buttons.Children.Add(b);
            }
            Button("OpenFull",Open);
            Button("PauseResume",async()=>{await vm!.PlayPauseCommand.ExecuteAsync(null);Log(new{kind="command",name="PlayPause"});});
            Button("Seek20",()=>Seek(20));
            Button("Seek21",()=>Seek(21));
            Button("Redraw",()=>{vm!.Danmaku.Redraw();return Task.CompletedTask;});
            Button("HideDanmaku",()=>{vm!.Danmaku.IsShowDanmaku=false;return Task.CompletedTask;});
            Button("ShowDanmaku",()=>{vm!.Danmaku.IsShowDanmaku=true;return Task.CompletedTask;});
            Button("Seek25",()=>Seek(25));
            Button("Back1",async()=>{await vm!.SkipBackwardCommand.ExecuteAsync(null);Log(new{kind="command",name="Back1",position=vm.Player.Position});});
            Button("Forward1",async()=>{await vm!.SkipForwardCommand.ExecuteAsync(null);Log(new{kind="command",name="Forward1",position=vm.Player.Position});});
            Button("Repeated",async()=>{await Seek(28);await Seek(24);await Seek(20);});
            Button("CloseFull",async()=>{if(vm!=null){var old=vm;Log(new{kind="close-enter",pendingInitialization=!(Field(old,"_initializationTask") as Task)!.IsCompleted,old.IsConnecting});await old.CloseAsync();Log(new{kind="closed",disposed=old.Client?.IsDisposed,remaining=GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().Players.Count});}});
            Button("NormalSpeed",async()=>{await vm!.IncreaseSpeedCommand.ExecuteAsync(null);Log(new{kind="normal-speed-command",speed=vm.Player?.PlaybackRate});});
            Button("Replay",async()=>{await vm!.Player.ReplayAsync();vm.IsControlsVisible=true;Log(new{kind="replayed",state=vm.Player.PlaybackState.ToString()});});
            Button("Remount",Remount);
            Button("SeedSubtitle",()=>{vm!.Subtitles.Clear();vm.Subtitles.Add(new BiliCopilot.UI.ViewModels.Items.SubtitleItemViewModel(new Richasy.BiliKernel.Models.Subtitle.SubtitleMeta("5759","Review subtitle 5759","offline"),_=>Task.CompletedTask,_=>Task.CompletedTask));vm.IsSubtitleEmpty=false;vm.IsControlsVisible=true;return Task.CompletedTask;});
            Button("OverlaySnapshot",()=>{SnapshotOverlay("manual");return Task.CompletedTask;});
            Button("ShowControls",()=>{vm!.IsControlsVisible=true;return Task.CompletedTask;});
            Button("SpeedClose",async()=>{var old=vm!;if(old.Player?.PlaybackState.ToString()!="Playing")throw new InvalidOperationException("SpeedClose requires Playing");var tasks=Enumerable.Range(0,20).Select(i=>old.ChangeSpeedAsync(i%2==0?1.5:2)).ToArray();Log(new{kind="speed-close-enter",state=old.Player?.PlaybackState.ToString(),pending=tasks.Count(t=>!t.IsCompleted)});await old.CloseAsync();await Task.WhenAll(tasks);Log(new{kind="speed-close-done",disposed=old.Client?.IsDisposed,completed=tasks.All(t=>t.IsCompleted)});});
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
    static async Task Seek(double target){var before=vm!.Player.Position;await vm.ChangePositionAsync(target);Log(new{kind="command",name="ChangePositionAsync",before,target,after=vm.Player.Position});}
    static OverlayTransportControls? retainedOverlay;
    static int mounts;
    static void SnapshotOverlay(string stage) {
        var c=retainedOverlay??Desc(vm!.Window!.XamlRoot.Content).OfType<OverlayTransportControls>().Single();
        var repeater=Field(c,"SubtitleRepeater") as ItemsRepeater;
        Log(new{kind="overlay-snapshot",stage,instance=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c),active=Field(c,"_isOverlayActive"),speedQueue=(Field(c,"_speedChangeList") as System.Collections.ICollection)?.Count,progressQueue=(Field(c,"_progressChangeList") as System.Collections.ICollection)?.Count,speedTimer=(Field(c,"_speedChangeTimer") as DispatcherTimer)?.IsEnabled,progressTimer=(Field(c,"_progressChangeTimer") as DispatcherTimer)?.IsEnabled,subtitleCreated=repeater!=null,subtitleSourceMatches=repeater!=null&&ReferenceEquals(repeater.ItemsSource,c.ViewModel.Subtitles),subtitleSourceNull=repeater?.ItemsSource==null,state=vm!.Player.PlaybackState.ToString(),rate=vm.Player.PlaybackRate});
    }
    static async Task Remount() {
        var c=retainedOverlay??=Desc(vm!.Window!.XamlRoot.Content).OfType<OverlayTransportControls>().Single();
        var parent=VisualTreeHelper.GetParent(c) as Panel??throw new InvalidOperationException("Expected panel");
        var index=parent.Children.IndexOf(c);
        SnapshotOverlay("before-unload");
        parent.Children.RemoveAt(index);
        await Task.Delay(750);
        SnapshotOverlay("unloaded");
        parent.Children.Insert(index,c);
        await Task.Delay(750);
        vm!.IsControlsVisible=true;
        SnapshotOverlay("reloaded-"+(++mounts));
    }
    static async Task Open()
    {
        View=new(Video,new("224"),[new(new("225","Offline part",Cover),1,60)],tags:[],operation:new("224",false,false,false));
        retainedOverlay=null;
        vm=GlobalDependencies.Kernel.GetRequiredService<PlayerViewModel>();
        vm.Danmaku.SeekRequested+=(_,target)=>Log(new{kind="seek-request",target});
        vm.Danmaku.RequestRedrawDanmaku+=(_,_)=>Log(new{kind="redraw-request",position=vm.Danmaku.Position});
        GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().Players.Add(vm);
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
            "GetSegmentDanmakusWithGrpcAsync"=>(IReadOnlyList<DanmakuInformation>)new[] { new DanmakuInformation("prior","PRIOR19",5,19,0xffffff,25), new DanmakuInformation("future","FUTURE21",5,21,0xffffff,25) },
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
