using System.Reflection;
using System.Text.Json;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Automation;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.Danmaku;
using Richasy.WinUIKernel.Share.Toolkits;
using RichasyKernel;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Media;
using Richasy.BiliKernel.Authenticator;
using System.Net;
using System.Net.Http;

namespace BiliCopilot.UI;

// DispatchProxy is used only for public service interfaces, never to access VM state.
public class OfflineProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Call = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!, args!);
    public static object Make(Type type, Func<MethodInfo, object?[], object?> call)
    { var proxy = Create(type, typeof(OfflineProxy)); ((OfflineProxy)proxy).Call=call; return proxy; }
}

internal static class Offline48
{
    static readonly Dictionary<string,object?> Settings=new();
    public static readonly List<object> Results=[];
    public static readonly List<string> Errors=[];
    public static readonly ControlledDanmaku Service=new();
    public static VideoPlayerView Video=null!;
    public static PgcPlayerView Pgc=null!;
    static Window Host=null!;
    static TextBlock Status=null!;
    static bool Running;
    static TaskCompletionSource? Continue;
    public static string Folder => Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.Path;
    public static void Log(string kind, object data) => File.AppendAllText(Path.Combine(Folder,"events.jsonl"),JsonSerializer.Serialize(new {kind,data})+Environment.NewLine);
    public static IKernelBuilder WithOffline48(this IKernelBuilder builder)
    {
        // Override every public Bili service boundary before Kernel.Build. No real auth resolver is resolved.
        foreach(var type in typeof(IPlayerService).Assembly.GetTypes().Where(t=>t.IsInterface && t.Namespace?.StartsWith("Richasy.BiliKernel.Bili") == true))
            builder.Services.AddSingleton(type, OfflineProxy.Make(type, Call));
        builder.Services.AddSingleton<IDanmakuService>(Service);
        Settings["HideMainWindowOnPlay"]=false;
        Settings["UseIntegrationWhenSinglePlayWindow"]=false;
        Settings["ShouldReportProgress"]=false;
        Settings["SponsorBlockEnabled"]=false;
        Settings["PlayerVolume"]=0d;
        builder.Services.AddSingleton((ISettingsToolkit)OfflineProxy.Make(typeof(ISettingsToolkit),(m,a)=>m.Name switch {
            "ReadLocalSetting" => Settings.TryGetValue((string)a[0]!,out var v)?v:a[1],
            "WriteLocalSetting" => Put((string)a[0]!,a[1]),
            "DeleteLocalSetting" => Delete((string)a[0]!),
            "IsSettingKeyExist" => Settings.ContainsKey((string)a[0]!), _ => null }));
        builder.Services.AddLogging(b=>b.AddProvider(new CaptureLogger()));
        return builder;
    }
    static object? Put(string k,object? v){Settings[k]=v;return null;}
    static object? Delete(string k){Settings.Remove(k);return null;}
    static object? Call(MethodInfo m,object?[] a)
    {
        Log("boundary", new {method=m.Name});
        object? value=m.Name switch {
            "GetVideoPageDetailAsync"=>Video,
            "GetPgcPageDetailAsync"=>Pgc,
            "GetVideoPlayDetailAsync" or "GetPgcPlayDetailAsync"=>new DashMediaInformation(
                [new("16",Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","offline.wav"),16000,"audio/wav","avc1",640,360,"","")],null,[new(16,"Offline PCM",false,false)],120),
            "GetToken"=>new BiliToken { UserId=900001, AccessToken="OFFLINE-NOT-A-CREDENTIAL" },
            "GetCookieString"=>string.Empty,
            "GetMyProfileAsync"=>new UserDetailProfile(new("900001","Offline",Cover)),
            _=>null
        };
        if(m.ReturnType==typeof(Task))return Task.CompletedTask;
        if(m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition()==typeof(Task<>)){
            var type=m.ReturnType.GenericTypeArguments[0];
            value ??= Empty(type);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type).Invoke(null,[value]);
        }
        if(value!=null)return value;
        if(m.ReturnType==typeof(void))return null;
        return Empty(m.ReturnType);
    }
    static object? Empty(Type type)
    {
        if(type.IsGenericType && type.GetGenericArguments().Length==1 && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GenericTypeArguments));
        return type.IsValueType?Activator.CreateInstance(type):null;
    }
    static BiliImage Cover=>new(new Uri("ms-appx:///Assets/Logo/Square150x150Logo.scale-100.png"));
    public static void SetModels(int duration=7200,int partDuration=7200)
    {
        var info=new VideoInformation(new("800001","OFFLINE article 7200s",Cover),new PublisherProfile(new("900002","Fixture author",Cover)),duration,"BV-offline",DateTimeOffset.UnixEpoch,communityInformation:new("800001"));
        Video=new(info,new UserCommunityInformation(),[new(new("810001","P1 "+partDuration+"s",Cover),1,partDuration),new(new("810002","P2 "+partDuration+"s",Cover),2,partDuration)],tags:[],operation:new("800001",false,false,false),aspectRatio:new(640,360));
        var season=new SeasonInformation(new("820001","OFFLINE PGC",Cover),new("820001"));
        var episodes=new List<EpisodeInformation>();
        for(var i=1;i<=2;i++){
            var ep=new EpisodeInformation(new((830000+i).ToString(),"Episode "+i,Cover),120,i);
            ep.AddExtensionIfNotNull(EpisodeExtensionDataId.Aid,840000L+i);
            ep.AddExtensionIfNotNull(EpisodeExtensionDataId.Cid,850000L+i);
            ep.AddExtensionIfNotNull(EpisodeExtensionDataId.AspectRatio,new MediaAspectRatio(640,360));
            episodes.Add(ep);
        }
        Pgc=new(season,episodes:episodes);
    }
    public static void Start()
    {
        SetModels();
        GlobalDependencies.Kernel.GetRequiredService<AccountViewModel>().MyProfile=new UserDetailProfile(new("900001","Offline",Cover),"fixture",1,false);
        Host=new Window {Title="OFFLINE 48 — real PlayerViewModel host"};
        Status=new TextBlock {Text="READY",TextWrapping=TextWrapping.Wrap};
        AutomationProperties.SetAutomationId(Status,"Status");
        var run=new Button {Content="Run offline VM acceptance"};AutomationProperties.SetAutomationId(run,"Run");
        var next=new Button {Content="Continue after screenshot"};AutomationProperties.SetAutomationId(next,"Continue");next.Click+=(_,_)=>Continue?.TrySetResult();
        run.Click+=async (_,_)=>{if(Running)return;Running=true;await Run();};
        Host.Content=new StackPanel {Padding=new Thickness(24),Spacing=16,Children={new TextBlock {Text="Offline #48 / production VM + connectors + dispatcher"},Status,run,next}};
        Host.AppWindow.Resize(new Windows.Graphics.SizeInt32(760,300));Host.Activate();
    }
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static async Task Wait(Func<bool> predicate,string description)
    { var end=DateTime.UtcNow.AddSeconds(20);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException(description);await Task.Delay(20);} }
    static async Task Drain(){var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);DispatcherQueue.GetForCurrentThread().TryEnqueue(DispatcherQueuePriority.Low,()=>t.SetResult());await t.Task;}
    static async Task Capture(string name,PlayerViewModel? vm=null)
    {
        if(vm!=null)vm.IsControlsVisible=true;
        Continue=new(TaskCreationOptions.RunContinuationsAsynchronously);
        File.WriteAllText(Path.Combine(Folder,"capture.json"),JsonSerializer.Serialize(new{name,hwnd=vm?.Window?.Handle.ToInt64(),hostHwnd=WinRT.Interop.WindowNative.GetWindowHandle(Host).ToInt64()}));
        Status.Text="CAPTURE "+name;
        await Continue.Task.WaitAsync(TimeSpan.FromMinutes(3));
        Status.Text="RUNNING";
    }
    static async Task Case(string name,Func<Task> test){try{await test();Results.Add(new{name,status="PASS"});}catch(Exception e){Results.Add(new{name,status="FAIL",detail=e.ToString()});}Log("case",Results.Last());}
    static async Task<PlayerViewModel> Open(bool pgc=false)
    {
        var vm=GlobalDependencies.Kernel.GetRequiredService<PlayerViewModel>();
        GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().Players.Add(vm);
        await vm.InitializeCommand.ExecuteAsync(pgc?new MediaSnapshot(Pgc.Information,Pgc.Episodes![0]):new MediaSnapshot(Video.Information,true));
        await Wait(()=>vm.Player?.IsPlaybackInitialized==true,"real playback initialize: "+vm.ErrorMessage);
        return vm;
    }
    static async Task Run()
    {
        Status.Text="RUNNING";
        await Case("VideoPartDurationBounds",async()=>{
            Service.Reset(false);SetModels(7200,120);var vm=await Open();
            try {
                await Wait(()=>Service.Calls.Count>0 && vm.Danmaku.GetCachedDanmakus().Count>0 && !vm.IsDanmakuLoading,"planned service calls");await Drain();
                Log("duration",new{article=Video.Information.Duration,part=Video.Parts![0].Duration,indices=Service.Calls.Select(c=>c.Index).ToArray(),count=vm.Danmaku.GetCachedDanmakus().Count,vm.Danmaku.DanmakuCountText});
                await Capture("duration",vm);
                Check(Service.Calls.Select(c=>c.Index).SequenceEqual(new[]{1}),"Current 120-second part must request only segment 1; observed "+string.Join(",",Service.Calls.Select(c=>c.Index)));
            } finally {vm.Close();await Drain();}
        });
        await Case("VideoSwitchLateSuccessAndError",()=>Switch(false));
        await Case("PgcSwitchLateSuccessAndError",()=>Switch(true));
        await Case("RepeatedReloadLateSuccessAndError",Reload);
        await Case("DirectDisposeInFlight",()=>Close(false));
        await Case("WindowDestroyInFlight",()=>Close(true));
        await Case("VmCancellationReachesHttpHandler",TransportCancellation);
        await Case("EmptyBodyRetriesThreeTimes",()=>Body(false));
        await Case("FiveByteEmptyFrameParsesOnce",()=>Body(true));
        await Capture("results");
        Status.Text="DONE";
        File.WriteAllText(Path.Combine(Folder,"results.json"),JsonSerializer.Serialize(Results,new JsonSerializerOptions{WriteIndented=true}));
    }
    static async Task FinishOld(SegmentCall[] old)
    {
        for(var i=old.Length-1;i>=0;i--){old[i].Fail=i%2==0;old[i].Release.TrySetResult();}
        await Task.WhenAll(old.Select(c=>c.Returned.Task)).WaitAsync(TimeSpan.FromSeconds(10));
        await Drain();
    }
    static void AssertCurrent(PlayerViewModel vm,int generation,int expected)
    {
        var items=vm.Danmaku.GetCachedDanmakus();
        Check(items.Count==expected,"item count "+items.Count+" expected "+expected);
        Check(items.All(x=>x.Content.StartsWith(generation+":")),"old content was published");
        var text=expected>0?string.Format(ResourceToolkit.GetLocalizedString(StringNames.DanmakuCountTemplate),expected.ToString("N0")):ResourceToolkit.GetLocalizedString(StringNames.DanmakuEmpty);
        Check(vm.Danmaku.DanmakuCountText==text,"CountText mismatch");
        Check(!vm.IsDanmakuLoading,"current load remained loading");
        Check(!Errors.Any(e=>e.Contains("SYNTHETIC-OLD-SEGMENT-ERROR")),"stale error was logged");
        Log("current",new{generation,count=items.Count,contents=items.Select(i=>i.Content),vm.Danmaku.DanmakuCountText,vm.IsDanmakuLoading});
    }
    static async Task Switch(bool pgc)
    {
        SetModels();
        if(pgc){var first=Pgc.Episodes![0];var longEp=new EpisodeInformation(first.Identifier,7200,1);longEp.AddExtensionIfNotNull(EpisodeExtensionDataId.Aid,840001L);longEp.AddExtensionIfNotNull(EpisodeExtensionDataId.Cid,850001L);longEp.AddExtensionIfNotNull(EpisodeExtensionDataId.AspectRatio,new MediaAspectRatio(640,360));Pgc.Episodes[0]=longEp;}
        Service.Reset(true);Errors.Clear();var vm=await Open(pgc);
        try {
            await Wait(()=>Service.Calls.Count==6 && vm.IsDanmakuLoading,"old six entered");
            var old=Service.Calls.ToArray();var oldTask=vm.InitializeDanmakuCommand.ExecutionTask!;Service.Generation=2;Service.Hold=false;
            if(pgc){var connector=(PgcConnectorViewModel)vm.Connector;connector.Sections.OfType<PgcPlayerEpisodeSectionDetailViewModel>().Single().Episodes![1].PlayCommand.Execute(null);}
            else {var connector=(VideoConnectorViewModel)vm.Connector;connector.Sections.OfType<VideoPlayerPartSectionDetailViewModel>().Single().SelectPartCommand.Execute(Video.Parts![1]);}
            await Wait(()=>old.All(c=>c.Token.IsCancellationRequested),"old token canceled");
            var expected=pgc?1:20;
            await Wait(()=>vm.Danmaku.GetCachedDanmakus().Count==expected && !vm.IsDanmakuLoading,"new source results");
            await FinishOld(old);await oldTask;await Drain();AssertCurrent(vm,2,expected);
            Check(Service.Calls.Count(c=>c.Generation==1)==6,"queued old segments started");
            Check(Service.Calls.Where(c=>c.Generation==2).All(c=>c.Cid==(pgc?"850002":"810002")),"wrong current identity");
            Log("switch",new{pgc,oldCount=old.Length,oldCanceled=old.All(c=>c.Token.IsCancellationRequested),currentDuration=pgc?Pgc.Episodes![1].Duration:Video.Parts![1].Duration,indices=Service.Calls.Where(c=>c.Generation==2).Select(c=>c.Index)});
            await Capture(pgc?"pgc-switch":"video-switch",vm);
        } finally {foreach(var c in Service.Calls)c.Release.TrySetResult();vm.Close();await Drain();}
    }
    static async Task Reload()
    {
        SetModels();Service.Reset(true);Errors.Clear();var vm=await Open();
        try {
            await Wait(()=>Service.Calls.Count==6 && vm.IsDanmakuLoading,"initial reload barrier");var first=Service.Calls.ToArray();var firstTask=vm.InitializeDanmakuCommand.ExecutionTask!;
            Service.Generation=2;var secondTask=vm.ReloadDanmakuCommand.ExecuteAsync(null);
            await Wait(()=>Service.Calls.Count(c=>c.Generation==2)==6,"second reload barrier");var second=Service.Calls.Where(c=>c.Generation==2).ToArray();
            Service.Generation=3;Service.Hold=false;await vm.ReloadDanmakuCommand.ExecuteAsync(null);
            await Wait(()=>vm.Danmaku.GetCachedDanmakus().Count==20 && !vm.IsDanmakuLoading,"third reload results");
            Check(first.Concat(second).All(c=>c.Token.IsCancellationRequested),"both old reload tokens canceled");
            await FinishOld(second);await secondTask;await FinishOld(first);await firstTask;await Drain();AssertCurrent(vm,3,20);
            Check(Service.Calls.Count(c=>c.Generation==1)==6 && Service.Calls.Count(c=>c.Generation==2)==6,"old queued reload requests started");
            await Capture("reload-command",vm);
        } finally {foreach(var c in Service.Calls)c.Release.TrySetResult();vm.Close();await Drain();}
    }
    static async Task Close(bool destroy)
    {
        SetModels();Service.Reset(true);Errors.Clear();var vm=await Open();
        await Wait(()=>Service.Calls.Count==6 && vm.IsDanmakuLoading,"close in-flight barrier");var old=Service.Calls.ToArray();var oldTask=vm.InitializeDanmakuCommand.ExecutionTask!;
        await Capture(destroy?"before-destroy":"before-dispose",vm);
        if(destroy)vm.Close();else await vm.DisposeAsync();
        await Wait(()=>old.All(c=>c.Token.IsCancellationRequested),"close tokens");await Drain();
        var count=vm.Danmaku.DanmakuCountText;var loading=vm.IsDanmakuLoading;
        await FinishOld(old);await oldTask;await vm.ReloadDanmakuCommand.ExecuteAsync(null);await vm.InitializeDanmakuCommand.ExecuteAsync(null);await Drain();
        Check(Service.Calls.Count==6,"close allowed queued or new requests");Check(vm.Danmaku.GetCachedDanmakus().Count==0,"closed VM got late content");
        Check(vm.Danmaku.DanmakuCountText==count && vm.IsDanmakuLoading==loading,"late work mutated closed VM state");
        Check(!Errors.Any(e=>e.Contains("SYNTHETIC-OLD-SEGMENT-ERROR")),"closed VM got stale errors");
        Log("closed",new{destroy,oldCanceled=old.All(c=>c.Token.IsCancellationRequested),count,loading,vm.Window!.IsClosed,vm.Window.IsDisposed});
    }
    static async Task TransportCancellation()
    {
        SetModels();Service.Reset(false);Errors.Clear();using var handler=new ControlledHandler(true,false);using var http=new BiliHttpClient(handler);
        Service.Transport=new DanmakuService(http,new BiliAuthenticator(null,null),null!);
        var vm=await Open();
        try {
            await Wait(()=>handler.Attempts==6 && vm.IsDanmakuLoading,"handler entered six");
            vm.Close();await handler.AllCanceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await vm.InitializeDanmakuCommand.ExecutionTask!;await Drain();
            Check(handler.Attempts==6 && handler.Cancellations==6,"cancellation retries or missing handler cancellation");
            Check(vm.Danmaku.GetCachedDanmakus().Count==0,"transport cancel published content");
            Log("transport-cancel",new{handler.Attempts,handler.Cancellations,errors=Errors.ToArray()});
        } finally {Service.Transport=null;vm.Close();}
    }
    static async Task Body(bool frame)
    {
        SetModels(120,120);Service.Reset(false);Errors.Clear();using var handler=new ControlledHandler(false,frame);using var http=new BiliHttpClient(handler);
        Service.Transport=new DanmakuService(http,new BiliAuthenticator(null,null),null!);var vm=await Open();
        try {
            await Wait(()=>handler.Attempts>0 && vm.Danmaku.DanmakuCountText==ResourceToolkit.GetLocalizedString(StringNames.DanmakuEmpty) && !vm.IsDanmakuLoading,"empty body terminal state");
            Check(handler.Attempts==(frame?1:3),"attempt count");
            var errorCount=Errors.Count(e=>e.Contains("获取弹幕分片失败"));Check(errorCount==(frame?0:1),"current error ownership");
            Check(vm.Danmaku.GetCachedDanmakus().Count==0,"empty frame/body content");
            Log("body",new{frame,bytes=frame?5:0,handler.Attempts,errorCount,vm.Danmaku.DanmakuCountText,vm.IsDanmakuLoading});
        } finally {Service.Transport=null;vm.Close();await Drain();}
    }
    sealed class CaptureLogger:ILoggerProvider
    {
        public ILogger CreateLogger(string name)=>new Capture(name);public void Dispose(){}
        sealed class Capture(string category):ILogger {
            public IDisposable? BeginScope<T>(T state) where T:notnull=>null;public bool IsEnabled(LogLevel level)=>level>=LogLevel.Warning;
            public void Log<T>(LogLevel level,EventId id,T state,Exception? error,Func<T,Exception?,string> formatter){if(level<LogLevel.Warning)return;var message=formatter(state,error);Errors.Add(message+" "+error?.Message);Offline48.Log("logger",new{category,level=level.ToString(),message,error=error?.ToString()});}
        }
    }
}

internal sealed class SegmentCall(string cid,int index,CancellationToken token)
{
    public string Cid=cid;public int Index=index;public CancellationToken Token=token;
    public TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Fail;
    public int Generation;
    public TaskCompletionSource Returned=new(TaskCreationOptions.RunContinuationsAsynchronously);
}
internal sealed class ControlledDanmaku:IDanmakuService
{
    public readonly System.Collections.Concurrent.ConcurrentQueue<SegmentCall> Calls=[];
    public bool Hold;
    public int Generation=1;
    public IDanmakuService? Transport;
    public void Reset(bool hold){Calls.Clear();Hold=hold;Generation=1;Transport=null;}
    public async Task<IReadOnlyList<DanmakuInformation>> GetSegmentDanmakusWithGrpcAsync(string aid,string cid,int segmentIndex,CancellationToken cancellationToken=default)
    {
        if(Transport!=null)return await Transport.GetSegmentDanmakusWithGrpcAsync(aid,cid,segmentIndex,cancellationToken);
        var call=new SegmentCall(cid,segmentIndex,cancellationToken){Generation=Generation};Calls.Enqueue(call);
        Offline48.Log("segment-enter",new{cid,index=segmentIndex});
        try {
            if(Hold)await call.Release.Task;
            if(call.Fail)throw new InvalidOperationException("SYNTHETIC-OLD-SEGMENT-ERROR");
            return new[]{new DanmakuInformation(call.Generation+"-"+cid+"-"+segmentIndex,call.Generation+":"+cid+"/"+segmentIndex,1,1,0xffffff,25)};
        }finally{call.Returned.TrySetResult();}
    }
    public Task<DanmakuMeta> GetDanmakuMetaAsync(string a,string c,CancellationToken t=default)=>throw new NotSupportedException();
    public Task<IReadOnlyList<DanmakuInformation>> GetSegmentDanmakusWithWebAsync(string a,string c,int i,CancellationToken t=default)=>throw new NotSupportedException();
    public Task SendVideoDanmakuAsync(string x,string a,string c,int p,string color,bool size=true,Richasy.BiliKernel.Models.DanmakuLocation location=Richasy.BiliKernel.Models.DanmakuLocation.Scroll,CancellationToken t=default)=>throw new NotSupportedException();
    public Task SendLiveDanmakuAsync(string x,string r,string color,bool size=true,Richasy.BiliKernel.Models.DanmakuLocation location=Richasy.BiliKernel.Models.DanmakuLocation.Scroll,CancellationToken t=default)=>throw new NotSupportedException();
}

internal sealed class ControlledHandler(bool hold,bool frame):HttpMessageHandler
{
    public int Attempts,Cancellations;
    public TaskCompletionSource AllCanceled=new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Attempts);
        if(hold){try{await Task.Delay(Timeout.Infinite,cancellationToken);}catch(OperationCanceledException){if(Interlocked.Increment(ref Cancellations)==6)AllCanceled.TrySetResult();throw;}}
        var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(frame?new byte[5]:Array.Empty<byte>())};
        response.Content.Headers.ContentType=new("application/grpc");return response;
    }
}
