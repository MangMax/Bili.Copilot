using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using BiliCopilot.UI.Controls.Danmaku;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Components;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Models.Danmaku;
using Richasy.Danmaku;
using Richasy.Danmaku.Legacy;
using RichasyKernel;
namespace BiliCopilot.UI;

// Diagnostic-only, compiled into the offline host; no reflection roots or changed engine DLLs.
internal static class SeekAot
{
    internal const string Root = @"FIXTURE_ROOT";
    static readonly BiliImage Cover = new(new Uri("ms-appx:///Assets/Logo/StoreLogo.png"));
    static readonly UserProfile User = new("224", "Synthetic offline account", Cover);
    static readonly VideoInformation Video = new(new("224", "Offline full player 224", Cover), new(User),60,"BVoffline224",DateTimeOffset.Now,communityInformation:new("224"));
    internal static readonly VideoPlayerView View = new(Video,new("224"),[new(new("225","Offline part",Cover),1,60)],tags:[],operation:new("224",false,false,false));
    static PlayerViewModel vm=null!;
    static readonly Stopwatch Clock=Stopwatch.StartNew();
    static double mediaAt=-1,progressAt=-1;
    internal static void MediaConfirmed(double target,bool succeeded) { if(succeeded){mediaAt=Clock.Elapsed.TotalMilliseconds;Log("media-command-return",target.ToString());} }
    static string LogPath => Path.Combine(Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.Path,"seek.tsv");
    static void Log(string name, string detail) => File.AppendAllText(LogPath,$"{Clock.Elapsed.TotalMilliseconds:F3}\t{name}\t{detail.Replace('\n',' ').Replace('\r',' ')}\n");
    static void Check(string name,bool pass,string detail="") { Log(pass?"PASS":"FAIL",name+" "+detail);if(!pass)throw new InvalidOperationException(name+" "+detail); }
    public static IKernelBuilder WithAotPlayback(this IKernelBuilder b) {
        b.Services.AddSingleton<IPlayerService,AotIPlayerService>(); b.Services.AddSingleton<ISubtitleService,AotISubtitleService>();
        b.Services.AddSingleton<IDanmakuService,AotIDanmakuService>(); b.Services.AddSingleton<IBiliTokenResolver,AotIBiliTokenResolver>(); b.Services.AddSingleton<IBiliCookiesResolver,AotIBiliCookiesResolver>();return b;
    }
    [UnsafeAccessor(UnsafeAccessorKind.Field,Name="_danmakuMaster")] static extern ref DanmakuFrostMaster? Legacy(VideoDanmakuPanel panel);
    [UnsafeAccessor(UnsafeAccessorKind.Field,Name="_danmakuRenderer")] static extern ref DanmakuRenderer? Core(VideoDanmakuPanel panel);
    [UnsafeAccessor(UnsafeAccessorKind.Field,Name="_lastTimeMs")] static extern ref uint LegacyTime(DanmakuFrostMaster engine);
    [UnsafeAccessor(UnsafeAccessorKind.Field,Name="_dirty")] static extern ref long Dirty(DanmakuRenderer engine);
    [UnsafeAccessor(UnsafeAccessorKind.Field,Name="_appliedDirty")] static extern ref long Applied(DanmakuRenderer engine);
    [UnsafeAccessor(UnsafeAccessorKind.Method,Name="FailWorker")] static extern void FailWorker(DanmakuFrostMaster engine,Exception error);
    static IEnumerable<DependencyObject> Desc(DependencyObject node) { yield return node; for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Desc(VisualTreeHelper.GetChild(node,i)))yield return child; }
    static VideoDanmakuPanel Panel()=>Desc(vm.Window!.XamlRoot.Content).OfType<VideoDanmakuPanel>().Single();
    static bool IsReady(uint target) { var panel=Panel();return Legacy(panel) is {} legacy ? legacy.WorkerFailure==null && Volatile.Read(ref LegacyTime(legacy))==target : Core(panel) is {} core && Interlocked.Read(ref Dirty(core))==Interlocked.Read(ref Applied(core)); }
    static async Task Ready(uint target) { var until=Clock.Elapsed.TotalSeconds+25;while(!IsReady(target)){if(Clock.Elapsed.TotalSeconds>until)throw new TimeoutException("Ready "+target);await Task.Delay(10);} }
    static async Task Open() {
        vm=GlobalDependencies.Kernel.GetRequiredService<PlayerViewModel>();
        GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().Players.Add(vm);
        await vm.InitializeAsync(new MediaSnapshot(Video,true){StartPosition=19});
        vm.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(vm.PositionText)){progressAt=Clock.Elapsed.TotalMilliseconds;Log("progress",vm.Player.Position+" "+vm.PositionText);}};
        vm.Player.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(vm.Player.Position))Log("media",vm.Player.Position.ToString());};
        await Task.Delay(2000);
        Check("RealLocalMedia",vm.Player.Duration>55 && vm.ErrorMessage is null or "",$"duration={vm.Player.Duration} error={vm.ErrorMessage}");
        if(vm.Player.PlaybackState.ToString()=="Playing")await vm.PlayPauseCommand.ExecuteAsync(null);
    }
    internal static async Task Run() {
        SettingsToolkit.WriteLocalSetting(SettingNames.HideMainWindowOnPlay,false);
        SettingsToolkit.WriteLocalSetting(SettingNames.ShouldReportProgress,false);
        SettingsToolkit.WriteLocalSetting(SettingNames.SponsorBlockEnabled,false);
        SettingsToolkit.WriteLocalSetting(SettingNames.CustomLibmpvPath,Path.Combine(Root,"libmpv-2.dll"));
        GlobalDependencies.Kernel.GetRequiredService<AccountViewModel>().MyProfile=new(User);
        foreach(var type in new[]{DanmakuRendererType.DirectX,DanmakuRendererType.Win2D}) {
            await Open();
            try {
                vm.Danmaku.Renderer=type;await Task.Delay(1000);
                vm.Danmaku.IsShowDanmaku=false;
                var dense=Enumerable.Range(0,12000).Select(i=>new DanmakuInformation("dense"+i,"TEXT "+i,(i%4) switch {0=>1,1=>6,2=>5,_=>4},54d*i/12000,0xffffff,25)).ToList();
                vm.Danmaku.Initialize(dense);await vm.ChangePositionAsync(20);vm.Danmaku.IsShowDanmaku=true;await Task.Delay(100);await Ready(20000);
                double last=Clock.Elapsed.TotalMilliseconds,gap=0;
                var timer=DispatcherQueue.GetForCurrentThread().CreateTimer();timer.Interval=TimeSpan.FromMilliseconds(16);
                timer.Tick+=(_,_)=>{var now=Clock.Elapsed.TotalMilliseconds;gap=Math.Max(gap,now-last);last=now;};timer.Start();
                try {
                    mediaAt=progressAt=-1;var begin=Clock.Elapsed.TotalMilliseconds;await vm.ChangePositionAsync(55);var returned=Clock.Elapsed.TotalMilliseconds;
                    await Ready(55000);var ready=Clock.Elapsed.TotalMilliseconds;await Task.Delay(150);
                    Log("Dense12000",$"engine={type} submitMs={returned-begin:F3} readyMs={ready-begin:F3} heartbeatMs={gap:F3} mediaAt={mediaAt:F3} progressAt={progressAt:F3} deltaMs={progressAt-mediaAt:F3}");
                    Check(type+"DenseSeekCommit",returned-begin<100);Check(type+"Heartbeat",gap<200);Check(type+"MediaProgress",mediaAt>=begin && progressAt>=mediaAt && progressAt-mediaAt<200);
                    var first=vm.ChangePositionAsync(45);await Task.Delay(10);var latest=vm.ChangePositionAsync(20);await Task.WhenAll(first,latest);await Ready(20000);await Task.Delay(150);
                    Check(type+"LatestTarget",Math.Abs(vm.Player.Position-20)<0.1);
                    await vm.ChangePositionAsync(54);var hide=Clock.Elapsed.TotalMilliseconds;vm.Danmaku.IsShowDanmaku=false;
                    Check(type+"HideResponsive",Clock.Elapsed.TotalMilliseconds-hide<100);await vm.ChangePositionAsync(23);await Task.Delay(150);
                    vm.Danmaku.IsShowDanmaku=true;await Task.Delay(100);await Ready(23000);Check(type+"ShowLatest",Math.Abs(vm.Player.Position-23)<0.1);
                    var paused=vm.Player.Position;await Task.Delay(400);Check(type+"Paused",Math.Abs(vm.Player.Position-paused)<0.1 && vm.Danmaku.IsPaused);
                    await vm.PlayPauseCommand.ExecuteAsync(null);await Task.Delay(1000);Check(type+"Resumed",vm.Player.Position>paused+0.3 && !vm.Danmaku.IsPaused);await vm.PlayPauseCommand.ExecuteAsync(null);
                } finally {timer.Stop();}
                // Force a pending rebuild using a different target, observe before joining Close.
                await vm.ChangePositionAsync(55);var pending=!IsReady(55000);var close=Clock.Elapsed.TotalMilliseconds;await vm.CloseAsync();
                Log("PendingClose",$"engine={type} pending={pending} ms={Clock.Elapsed.TotalMilliseconds-close:F3}");
                Check(type+"PendingClose",pending && vm.Window!.IsClosed && Clock.Elapsed.TotalMilliseconds-close<500);
            } finally {await vm.CloseAsync();}
        }
        Log("DONE","normal bounded AOT seek");
        // This replay explicitly includes the previously failed terminal-state scenario.

        // Safe terminal-state injection: publishes the real failure protocol, no native resource corruption.
        await Open();try {
            vm.Danmaku.Renderer=DanmakuRendererType.Win2D;await Task.Delay(1000);var engine=Legacy(Panel())!;
            var fault=new InvalidOperationException("Controlled diagnostic terminal state");FailWorker(engine,fault);
            bool rejected=false;try{engine.Seek(20000);}catch(InvalidOperationException e){rejected=ReferenceEquals(e.InnerException,fault);}
            Check("FaultRejectsFurtherSeek",rejected && ReferenceEquals(engine.WorkerFailure,fault));
            var start=Clock.Elapsed.TotalMilliseconds;
            await vm.ChangePositionAsync(25);await Task.Delay(250);
            Check("FaultMediaProgress",Math.Abs(vm.Player.Position-25)<0.2 && vm.PositionText.Contains("25") && Clock.Elapsed.TotalMilliseconds-start<1000);
            Check("FaultRendererRetired",Legacy(Panel()) is null);
            await vm.ChangePositionAsync(35);await Task.Delay(200);
            Check("FaultSubsequentSeek",Math.Abs(vm.Player.Position-35)<0.2 && vm.PositionText.Contains("35"));
            vm.Danmaku.ResetStyle();vm.Danmaku.ExtraSpeed=1.1;vm.Danmaku.Redraw();
            vm.Danmaku.IsShowDanmaku=false;await Task.Delay(100);
            Check("FaultHidden",!vm.Danmaku.IsShowDanmaku && Legacy(Panel()) is null);
            vm.Danmaku.IsShowDanmaku=true;await Task.Delay(200);
            Check("FaultDoesNotRetry",Legacy(Panel()) is null);
            await vm.PlayPauseCommand.ExecuteAsync(null);await Task.Delay(1000);
            Check("FaultMediaContinues",vm.Player.Position>35.3 && !vm.Danmaku.IsPaused);
            await vm.PlayPauseCommand.ExecuteAsync(null);
            await vm.CloseAsync();
            Check("FaultUnloadAndClose",vm.Window!.IsClosed);
        } finally {await vm.CloseAsync();}
        await Open();try {
            vm.Danmaku.Renderer=DanmakuRendererType.Win2D;vm.Danmaku.IsShowDanmaku=true;vm.Danmaku.Redraw();await Task.Delay(1000);
            Check("NewPlaybackRecovers",Legacy(Panel()) is {} fresh && fresh.WorkerFailure is null);
            await vm.ChangePositionAsync(28);await Ready(28000);
            // MPV command completion / renderer readiness precede the observable media event.
            var until=Clock.Elapsed.TotalMilliseconds+1000;
            while((Math.Abs(vm.Player.Position-28)>=0.2 || !vm.PositionText.Contains("28")) && Clock.Elapsed.TotalMilliseconds<until)await Task.Delay(10);
            Check("RecoveredSeek",Math.Abs(vm.Player.Position-28)<0.2 && vm.PositionText.Contains("28"),$"position={vm.Player.Position} progress={vm.PositionText}");
        } finally {await vm.CloseAsync();}
        Log("DONE","bounded AOT seek and terminal isolation");
    }
}
