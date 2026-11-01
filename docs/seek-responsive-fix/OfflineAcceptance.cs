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
    internal const string Root = @"C:\Users\hp\.t3\worktrees\Bili.Copilot\t3-d1fc3815\artifacts\seek-fix\full";
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
    static bool loadedModulesLogged;
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
        var core=Field(panel,"_danmakuRenderer");
        var surface=core==null?null:Field(Field(core,"_owner")!,"surface");
        var native=master!=null?Field(master,"_render"):surface==null?null:Field(surface,"renderer");
        if(native==null){Log(new{kind="visual",panel=true,renderer=false});return;}
        if(!loadedModulesLogged && AppDomain.CurrentDomain.GetAssemblies().Count(a=>a.GetName().Name!.StartsWith("Danmaku."))==3) {
            Log(new{kind="loaded-modules",pid=Environment.ProcessId,modules=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name!.StartsWith("Danmaku.")).Select(a=>new{name=a.GetName().Name,path=a.Location,mvid=a.ManifestModule.ModuleVersionId,sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location)))}).ToArray()});
            loadedModulesLogged=true;
        }
        var rows=new List<object>();
        var total=surface==null?TimeSpan.Zero:(TimeSpan)(Field(surface,"total")??TimeSpan.Zero);
        if(Field(native,"_renderLayerList") is Array layers)foreach(var layer in layers){
            if(Field(layer!,"RenderList") is System.Collections.IList list)lock(list)foreach(var item in list){
                var model=Field(item!,"Item");
                var first=(TimeSpan)(Field(item!,"FirstRenderTime")??TimeSpan.Zero);
                rows.Add(new{text=model==null?Field(item!,"Text"):Field(model,"Text")??model.GetType().GetProperty("Text")?.GetValue(model),x=Field(item!,"X"),y=Field(item!,"StartY"),width=Field(item!,"Width"),height=Field(item!,"Height"),age=core!=null?(total-first).TotalMilliseconds:Field(item!,"ReconstructedAgeMs"),first=first.TotalMilliseconds,drawable=Field(item!,"IsFirstRenderTimeSet")});
            }
        }
        object? Val(string n)=>Field(native,n)??native.GetType().GetProperty(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(native);
        Log(new{kind="visual",panel=true,renderer=true,engine=core!=null?"Core":"Legacy",master=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(core??master!),paused=Val("_isPaused"),stopped=Val("_isStopped"),time=master==null?vm.Danmaku.Position*1000:Field(master,"_lastTimeMs"),width=Val("CanvasWidth"),height=Val("CanvasHeight"),scale=Val("_scale"),speed=vm.Danmaku.DanmakuSpeed,extra=vm.Danmaku.ExtraSpeed,font=vm.Danmaku.DanmakuFontSize,rows});
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
            Log(new{kind="modules",modules=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name!.StartsWith("Danmaku.")).Select(a=>new{name=a.GetName().Name,path=a.Location,mvid=a.ManifestModule.ModuleVersionId,sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location)))}).ToArray()});
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
            control=new Window {Title="Seek pause 2bd22 controller"};
            var buttons=new StackPanel();
            void Button(string id,Func<Task> action) {
                var b=new Button {Content=id};Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(b,id);
                b.Click+=async (_,_)=>{try{await action();}catch(Exception ex){Log(new{kind="action-error",id,error=ex.ToString()});}};buttons.Children.Add(b);
            }
            Button("OpenFull",Open);
            Button("PauseResume",async()=>{await vm!.PlayPauseCommand.ExecuteAsync(null);Log(new{kind="command",name="PlayPause"});});
            Button("Core",()=>{vm!.Danmaku.Renderer=DanmakuRendererType.DirectX;return Task.CompletedTask;});
            Button("Legacy",()=>{vm!.Danmaku.Renderer=DanmakuRendererType.Win2D;return Task.CompletedTask;});
            Button("Seek19",()=>Seek(19));
            Button("Seek22",()=>Seek(22));
            Button("Seek23",()=>Seek(23));
            Button("Seek24",()=>Seek(24));
            Button("QueuedClose",async()=>{var old=vm!;var pending=Task.WhenAll(old.ChangePositionAsync(21),old.ChangePositionAsync(19),old.ChangePositionAsync(20));await old.CloseAsync();await pending;Log(new{kind="closed",disposed=old.Client?.IsDisposed,queuedSeek=true});});
            Button("FastLatest",async()=>{await Task.WhenAll(Seek(21),Seek(19),Seek(20));});
            Button("Config",()=>{vm!.Danmaku.DanmakuSpeed=1;vm.Danmaku.DanmakuFontSize=1;vm.Danmaku.IsRollingEnabled=true;vm.Danmaku.IsTopEnabled=true;vm.Danmaku.IsBottomEnabled=true;vm.Danmaku.Redraw();return Task.CompletedTask;});
            Button("Blank",()=>{vm!.Danmaku.IsRollingEnabled=false;vm.Danmaku.IsTopEnabled=false;vm.Danmaku.IsBottomEnabled=false;vm.Danmaku.Redraw();return Task.CompletedTask;});
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
            control.Content=new ScrollViewer {Content=buttons};control.Activate();
            observer=DispatcherQueue.GetForCurrentThread().CreateTimer();observer.Interval=TimeSpan.FromMilliseconds(250);
            observer.Tick+=(_,_)=>{try{Log(new{kind="state",position=vm?.Player?.Position,state=vm?.Player?.PlaybackState.ToString(),speed=vm?.Player?.PlaybackRate,connecting=vm?.IsConnecting,error=vm?.ErrorMessage,danmakuPosition=vm?.Danmaku.Position,danmakuPaused=vm?.Danmaku.IsPaused,xload=vm?.IsDanmakuControlVisible,closed=vm?.Window?.IsClosed});ObserveVisual();}catch(Exception ex){Log(new{kind="observe-error",error=ex.Message});}};
            observer.Start();await Open(); await Task.Delay(2500); await Supplement(app);
        }
        catch(Exception ex){Log(new{kind="startup-error",error=ex.ToString()});}
    }
    internal static bool HoldDanmaku { get; set; }
    static void Check(string name,bool passed,object? detail=null) => Log(new{kind="assert",name,passed,detail});
    static object? Engine() {
        var panel=Desc(vm!.Window!.XamlRoot.Content).OfType<VideoDanmakuPanel>().Single();
        return Field(panel,"_danmakuRenderer")??Field(panel,"_danmakuMaster");
    }
    static object? EngineList(object engine) => engine.GetType().GetMethod("GetDanmakuList")!.Invoke(engine,null);
    static async Task Supplement(App app) {
        observer?.Stop();
        try {
            if(vm!.Player.PlaybackState.ToString()=="Playing")await vm.PlayPauseCommand.ExecuteAsync(null);
            vm.Danmaku.Renderer=DanmakuRendererType.Win2D;await Task.Delay(1000);ObserveVisual();
            await VisiblePerf(DanmakuRendererType.Win2D);
            await Lifetime(DanmakuRendererType.Win2D);
            await LongAndClose(DanmakuRendererType.Win2D);
        } catch(Exception e) {Log(new{kind="supplement-error",error=e.ToString()});}
        finally {await vm!.CloseAsync();observer?.Stop();app.ReleaseTrayResources();control!.Close();app.Exit();}
    }
    static async Task Ready(uint target) {
        var e=Engine()!;
        if(e is Richasy.Danmaku.DanmakuRenderer) {
            var until=DateTime.UtcNow.AddSeconds(20);
            while(DateTime.UtcNow<until) {
                var operation=(Task)Field(e,"_lastOperation")!;
                await operation.WaitAsync(TimeSpan.FromSeconds(20));
                if(ReferenceEquals(operation,Field(e,"_lastOperation"))&&operation.GetType().GetProperty("Result")!.GetValue(operation)!.ToString()=="Applied"
                    &&(uint)Field(Field(e,"_scheduler")!,"_lastTimeMs")! == target)return;
                await Task.Delay(10);
            }
            throw new TimeoutException("Core ready "+target);
        } else {
            var until=DateTime.UtcNow.AddSeconds(20);
            while(DateTime.UtcNow<until) {
                if((uint)Field(e,"_lastTimeMs")! == target && !(bool)Field(Field(e,"_render")!,"_isStopped")!)return;
                await Task.Delay(10);
            }
            throw new TimeoutException("Legacy ready "+target);
        }
    }
    static async Task VisiblePerf(DanmakuRendererType type) {
        var watch=System.Diagnostics.Stopwatch.StartNew();double last=0,gap=0;
        var timer=DispatcherQueue.GetForCurrentThread().CreateTimer();timer.Interval=TimeSpan.FromMilliseconds(16);
        timer.Tick+=(_,_)=>{var now=watch.Elapsed.TotalMilliseconds;gap=Math.Max(gap,now-last);last=now;};timer.Start();
        try {
            foreach(var count in new[]{300,12000}) {
                vm!.Danmaku.IsShowDanmaku=false;
                var data=Enumerable.Range(0,count).Select(i=>new DanmakuInformation("visible"+i,"TEXT "+i,(i%4) switch {0=>1,1=>6,2=>5,_=>4},54d*i/count,0xffffff,25)).ToList();
                vm.Danmaku.Initialize(data);await vm.ChangePositionAsync(20);vm.Danmaku.IsShowDanmaku=true;
                await Task.Delay(50);await Ready(20000);await Task.Delay(50);
                last=watch.Elapsed.TotalMilliseconds;gap=0;var start=last;
                await vm.ChangePositionAsync(55);var returned=watch.Elapsed.TotalMilliseconds;
                await Ready(55000);var ready=watch.Elapsed.TotalMilliseconds;await Task.Delay(100);
                Log(new{kind="visible-perf",engine=type.ToString(),count,callMs=returned-start,readyMs=ready-start,gap});
                Check("VisibleMediaSeekResponsive",returned-start<100,new{type,count});
                Check("VisibleHeartbeatResponsive",gap<200,new{type,count,gap});
                var first=vm.ChangePositionAsync(45);await Task.Delay(10);
                var latest=vm.ChangePositionAsync(20);await Task.WhenAll(first,latest);await Ready(20000);await Task.Delay(200);
                Check("DenseRapidSeekLatest",Math.Abs(vm.Player.Position-20)<0.1,new{type,count});
                await vm.ChangePositionAsync(54);await Task.Delay(10);start=watch.Elapsed.TotalMilliseconds;
                vm.Danmaku.IsShowDanmaku=false;var hidden=watch.Elapsed.TotalMilliseconds;
                Log(new{kind="hide-during-rebuild",engine=type.ToString(),count,callMs=hidden-start});
                Check("HideDuringDenseRebuildResponsive",hidden-start<100,new{type,count});
                await Task.Delay(300);
            }
        } finally {timer.Stop();}
    }
    static Dictionary<string,double> ActivePositions() {
        var e=Engine()!;var surface=Field(e,"_owner") is {} owner?Field(owner,"surface"):null;
        var native=Field(e,"_render")??(surface==null?null:Field(surface,"renderer"));
        var result=new Dictionary<string,double>();
        if(native!=null&&Field(native,"_renderLayerList") is Array layers)foreach(var layer in layers) {
            if(Field(layer!,"RenderList") is System.Collections.IList list)lock(list)foreach(var item in list) {
                var model=Field(item!,"Item");var text=(string?)(model==null?Field(item!,"Text"):Field(model,"Text"));
                if(text!=null)result[text]=Convert.ToDouble(Field(item!,"X"));
            }
        }
        return result;
    }
    static async Task Lifetime(DanmakuRendererType type) {
        await vm!.ChangePositionAsync(55);vm.Danmaku.IsShowDanmaku=true;await Task.Delay(150);await Ready(55000);
        var e=Engine()!;
        var previousSpeed=vm.Danmaku.DanmakuSpeed;vm.Danmaku.DanmakuSpeed=0.02;vm.Danmaku.ResetStyle();
        var list=new List<Richasy.Danmaku.Models.DanmakuItem> {
            new(){Id="oldslow",Text="OLD_SLOW",StartMs=0,Mode=Richasy.Danmaku.Enums.DanmakuMode.Rolling,BaseFontSize=25,TextColor=Microsoft.UI.Colors.White},
            new(){Id="long",Text="LONG_DURATION",StartMs=0,DurationMs=60000,Mode=Richasy.Danmaku.Enums.DanmakuMode.Top,BaseFontSize=25,TextColor=Microsoft.UI.Colors.White},
            new(){Id="expired",Text="EXPIRED",StartMs=0,Mode=Richasy.Danmaku.Enums.DanmakuMode.Top,BaseFontSize=25,TextColor=Microsoft.UI.Colors.White},
            new(){Id="roll",Text="ROLL",StartMs=54000,Mode=Richasy.Danmaku.Enums.DanmakuMode.Rolling,BaseFontSize=25,TextColor=Microsoft.UI.Colors.White},
            new(){Id="reverse",Text="REVERSE",StartMs=54000,Mode=Richasy.Danmaku.Enums.DanmakuMode.ReverseRolling,BaseFontSize=25,TextColor=Microsoft.UI.Colors.White},
            new(){Id="short",Text="SHORT_DURATION",StartMs=55000,DurationMs=500,Mode=Richasy.Danmaku.Enums.DanmakuMode.Bottom,BaseFontSize=25,TextColor=Microsoft.UI.Colors.White}
        };
        e.GetType().GetMethod("SetDanmakuList")!.Invoke(e,[list]);vm.Danmaku.Seek(55);
        await Task.Delay(100);await Ready(55000);await Task.Delay(100);
        var before=ActivePositions();
        Check("MeasuredLifetimeContent",before.ContainsKey("LONG_DURATION")&&before.ContainsKey("ROLL")&&before.ContainsKey("REVERSE")&&before.ContainsKey("SHORT_DURATION")&&!before.ContainsKey("EXPIRED"),new{type,before});
        Check("SlowRollingHistoryIsNotClipped",before.ContainsKey("OLD_SLOW"),type.ToString());
        await Task.Delay(700);var after=ActivePositions();
        Check("NativePauseFreezesPositionAndAge",before.Count==after.Count&&before.All(x=>after.TryGetValue(x.Key,out var v)&&Math.Abs(v-x.Value)<0.01),new{type,before,after});
        await vm.PlayPauseCommand.ExecuteAsync(null);await Task.Delay(1100);after=ActivePositions();
        Check("NativeResumeExpiresShortButKeepsLong",!after.ContainsKey("SHORT_DURATION")&&after.ContainsKey("LONG_DURATION"),new{type,after});
        await vm.PlayPauseCommand.ExecuteAsync(null);vm.Danmaku.IsShowDanmaku=false;vm.Danmaku.DanmakuSpeed=previousSpeed;
    }
    static async Task LongAndClose(DanmakuRendererType type) {
        vm!.Danmaku.IsShowDanmaku=false;
        var dense=Enumerable.Range(0,12000).Select(i=>new DanmakuInformation("long"+i,"LONG "+i,(i%4) switch {0=>1,1=>6,2=>5,_=>4},3599d*i/12000,0xffffff,25)).ToList();
        vm.Danmaku.Initialize(dense);vm.Danmaku.IsShowDanmaku=true;await Task.Delay(100);await Ready((uint)(vm.Danmaku.Position*1000));
        var sw=System.Diagnostics.Stopwatch.StartNew();vm.Danmaku.Seek(3550);var admission=sw.Elapsed.TotalMilliseconds;
        await Task.Delay(50);await Ready(3550000);
        Log(new{kind="long-visible",engine=type.ToString(),count=12000,admissionMs=admission,readyMs=sw.Elapsed.TotalMilliseconds});
        Check("LongTimelineResponsive",admission<100,type.ToString());
        var e=Engine()!;var originalList=EngineList(e);
        vm.Danmaku.Seek(3500);await Task.Delay(10);
        var replacement=new List<Richasy.Danmaku.Models.DanmakuItem> {new(){Id="replacement",Text="REPLACEMENT_ONLY",StartMs=0,DurationMs=4000000,Mode=Richasy.Danmaku.Enums.DanmakuMode.Top,BaseFontSize=25,TextColor=Microsoft.UI.Colors.White}};
        e.GetType().GetMethod("SetDanmakuList")!.Invoke(e,[replacement]);vm.Danmaku.Seek(3550);await Task.Delay(100);await Ready(3550000);await Task.Delay(100);
        var content=ActivePositions();Check("ReplacedListRejectsOldRebuild",content.Count==1&&content.ContainsKey("REPLACEMENT_ONLY"),new{type,content});
        e.GetType().GetMethod("SetDanmakuList")!.Invoke(e,[originalList]);vm.Danmaku.Seek(3450);await Task.Delay(10);
        var pending=e is Richasy.Danmaku.DanmakuRenderer ? !((Task)Field(e,"_lastOperation")!).IsCompleted : (bool)Field(Field(e,"_render")!,"_isStopped")!;
        sw.Restart();await vm.CloseAsync();
        Log(new{kind="close-during-rebuild",engine=type.ToString(),pending,elapsedMs=sw.Elapsed.TotalMilliseconds});
        Check("CloseDuringRebuildResponsive",sw.Elapsed.TotalMilliseconds<500&&vm.Window!.IsClosed,new{type,pending});
    }
    static async Task Measure(App app)
    {
        var heartbeat = DispatcherQueue.GetForCurrentThread().CreateTimer();
        heartbeat.Interval = TimeSpan.FromMilliseconds(16);
        var wall = System.Diagnostics.Stopwatch.StartNew();
        double last = wall.Elapsed.TotalMilliseconds, maxGap = 0;
        heartbeat.Tick += (_,_) => { var now=wall.Elapsed.TotalMilliseconds;maxGap=Math.Max(maxGap,now-last);last=now; };
        heartbeat.Start();
        vm!.PropertyChanged += (_,e) => { if(e.PropertyName==nameof(vm.PositionText))Log(new{kind="progress-vm",ms=wall.Elapsed.TotalMilliseconds,vm.PositionText,position=vm.Player.Position}); };
        vm.Player.PropertyChanged += (_,e) => { if(e.PropertyName==nameof(vm.Player.Position))Log(new{kind="media-position",ms=wall.Elapsed.TotalMilliseconds,position=vm.Player.Position}); };
        try {
            if(vm.Player.PlaybackState.ToString()=="Playing")await vm.PlayPauseCommand.ExecuteAsync(null);
            foreach(var engine in new[]{DanmakuRendererType.Win2D,DanmakuRendererType.DirectX}) {
                vm.Danmaku.Renderer=engine; await Task.Delay(1200);
                foreach(var count in new[]{300,12000}) {
                    vm.Danmaku.IsShowDanmaku=false;
                    var data=Enumerable.Range(0,count).Select(i=>new DanmakuInformation(i.ToString(),"DENSE "+i, (i%4) switch {0=>1,1=>6,2=>5,_=>4}, 54d*i/count,0xffffff,25)).ToList();
                    maxGap=0; var begin=wall.Elapsed.TotalMilliseconds;
                    vm.Danmaku.Initialize(data); vm.Danmaku.IsShowDanmaku=false;
                    await Task.Delay(1500);
                    Log(new{kind="hidden-load",engine=engine.ToString(),count,elapsed=wall.Elapsed.TotalMilliseconds-begin,maxGap});
                    maxGap=0;begin=wall.Elapsed.TotalMilliseconds;
                    Log(new{kind="measure-start",engine=engine.ToString(),count,ms=begin});
                    await vm.ChangePositionAsync(55);
                    var returned=wall.Elapsed.TotalMilliseconds;
                    await Task.Delay(500);
                    Log(new{kind="hidden-seek",engine=engine.ToString(),count,callMs=returned-begin,maxGap,position=vm.Player.Position,vm.PositionText});
                    begin=wall.Elapsed.TotalMilliseconds;
                    vm.Danmaku.Seek(3600);
                    Log(new{kind="hidden-long-timeline",engine=engine.ToString(),count,callMs=wall.Elapsed.TotalMilliseconds-begin});
                    await vm.ChangePositionAsync(20);
                    await Task.Delay(500);
                }
            }
        } catch(Exception e) {Log(new{kind="measure-error",error=e.ToString()});}
        finally {heartbeat.Stop();await vm.CloseAsync();observer?.Stop();app.ReleaseTrayResources();control!.Close();app.Exit();}
    }
    static async Task Seek(double target){var before=vm!.Player.Position;Log(new{kind="request",target,before});await vm.ChangePositionAsync(target);Log(new{kind="command",name="ChangePositionAsync",before,target,after=vm.Player.Position});}
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
        vm!.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(vm.PositionText))Log(new{kind="progress-vm",position=vm.Player.Position,vm.PositionText});};
        vm.Player.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(vm.Player.Position))Log(new{kind="media-position",position=vm.Player.Position});};
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
            "GetSegmentDanmakusWithGrpcAsync"=>(IReadOnlyList<DanmakuInformation>)new[] { new DanmakuInformation("roll","ROLL18",1,18,0xffffff,25), new DanmakuInformation("rev","REV18",6,18,0xffffff,25), new DanmakuInformation("prior","PRIOR19",5,19,0xffffff,25), new DanmakuInformation("bottom","BOTTOM19",4,19,0xffffff,25), new DanmakuInformation("future","FUTURE21",5,21,0xffffff,25) },
            _=>null
        };
        var type=m.ReturnType;
        if(type==typeof(void))return null;
        if(type==typeof(Task))return Task.CompletedTask;
        if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(Task<>)) {
            var t=type.GenericTypeArguments[0];value??=Empty(t);
            if(m.Name=="GetSegmentDanmakusWithGrpcAsync"&&OfflineAcceptance.HoldDanmaku) return typeof(OfflineProxy).GetMethod(nameof(Held),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(t).Invoke(null,[value,OfflineAcceptance.ServiceGate!.Task]);
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


