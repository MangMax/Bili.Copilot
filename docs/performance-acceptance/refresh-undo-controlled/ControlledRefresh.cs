using System.Reflection;
using System.Text.Json;
using System.Collections.ObjectModel;
using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Controls.Popular;
using BiliCopilot.UI.Controls.VideoPartition;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.ViewModels.View;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Automation;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI;

// External discovery service barriers only. Production VM, item, command and controls are unchanged.
internal static class ControlledRefresh
{
    sealed class Request(string method, long offset)
    {
        public string Method=method;
        public long Offset=offset;
        public TaskCompletionSource<(IReadOnlyList<VideoInformation>,long)> Completion=new();
        public bool Released;
        public void Release(string[] ids,long cursor,bool fail=false)
        {
            Released=true;
            Write("response",new {Method,Offset,ids,cursor,fail});
            if(fail) Completion.SetException(new IOException("controlled batch failure"));
            else Completion.SetResult((ids.Select(Video).ToArray(),cursor));
        }
    }
    static Window host=null!;
    static Grid slot=new();
    static TextBlock status=new();
    static List<Request> requests=[];
    static List<object> results=[];
    static Dictionary<object,int> references=new(ReferenceEqualityComparer.Instance);
    static Driver d=null!;
    static bool running;
    static string current="setup";
    static TaskCompletionSource? continueGate;
    static string Folder=>Offline48.Folder;
    static void Write(string kind,object data)=>File.AppendAllText(Path.Combine(Folder,"controlled.jsonl"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,scenario=current,kind,data})+Environment.NewLine);
    static int Ref(object value){if(!references.TryGetValue(value,out var id)) references[value]=id=references.Count+1;return id;}
    static object? Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(value);
    static VideoInformation Video(string id)=>new(new(id,"Controlled item "+id,null),null,120,"BV-controlled-"+id);
    public static bool Handles(string name)=>name is "GetVideoPartitionsAsync" or "GetRecommendVideoListAsync" or "GetPartitionRecommendVideoListAsync" or "GetHotVideoListAsync" or "GetChildPartitionVideoListAsync";
    public static object Call(MethodInfo m,object?[] a)
    {
        if(m.Name=="GetVideoPartitionsAsync")return Task.FromResult<IReadOnlyList<Partition>>([]);
        var req=new Request(m.Name,(long)a[m.Name is "GetPartitionRecommendVideoListAsync" or "GetChildPartitionVideoListAsync"?1:0]!);
        requests.Add(req);Write("request",new{number=requests.Count,req.Method,req.Offset});
        if(m.Name=="GetChildPartitionVideoListAsync")return Child(req);
        return req.Completion.Task;
    }
    static async Task<(IReadOnlyList<VideoInformation>,long,int)> Child(Request r){var (items,cursor)=await r.Completion.Task;return(items,cursor,1);}
    public static void Start()
    {
        Offline48.Settings["IncrementalRecommendRefresh"]=true;
        Offline48.Settings["PopularPageLastSelectedSectionId"]="Recommend";
        host=new(){Title="Controlled refresh undo — offline production controls"};
        var root=new Grid{Padding=new Thickness(16),RowSpacing=8};
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};
        void Add(string id,Action action){var b=new Button{Content=id};AutomationProperties.SetAutomationId(b,id);b.Click+=(_,_)=>action();actions.Children.Add(b);}
        Add("RunControlled",()=>{if(!running){running=true;_=Run();}});
        Add("ContinueEvidence",()=>continueGate?.TrySetResult());
        Add("CloseOwned",()=>host.Close());
        AutomationProperties.SetAutomationId(status,"ControlledStatus");status.Text="READY";
        root.Children.Add(actions);Grid.SetRow(status,1);root.Children.Add(status);Grid.SetRow(slot,2);root.Children.Add(slot);
        host.Content=root;host.AppWindow.Resize(new(1200,900));host.Activate();
        Application.Current.UnhandledException+=(_,e)=>Write("unhandled",new{type=e.Exception.GetType().FullName});
    }
    sealed class Driver
    {
        public object Vm=null!;
        public ObservableCollection<VideoItemViewModel> Items=null!;
        public IAsyncRelayCommand Refresh=null!,Load=null!;
        public IRelayCommand Undo=null!;
        public Func<Task> Initialize=null!,Switch=null!;
        public Func<bool> Loading=null!,Refreshing=null!,CanUndo=null!;
        public FrameworkElement Body=null!,Header=null!;
        public IncrementVerticalScrollControl Increment=null!;
        public ScrollView Scroll=null!;
        public int Updates,Changes;
        public long Cursor=>(long)Field(Vm,"_recommendOffset")!;
    }
    static async Task Drain(){var gate=new TaskCompletionSource();DispatcherQueue.GetForCurrentThread().TryEnqueue(DispatcherQueuePriority.Low,()=>gate.SetResult());await gate.Task;}
    static async Task Until(Func<bool> predicate,string reason)
    {var deadline=DateTime.UtcNow.AddSeconds(20);while(!predicate()){if(DateTime.UtcNow>deadline)throw new TimeoutException(reason);await Task.Delay(16);}await Drain();}
    static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    static async Task<Request> Next(int index){await Until(()=>requests.Count>index,"service request "+index);return requests[index];}
    static void Snapshot(string stage)
    {
        var snap=Field(d.Vm,"_recommendRefreshSnapshot");
        var snapshotItems=snap?.GetType().GetProperty("Items")?.GetValue(snap) as IEnumerable<VideoItemViewModel>;
        Write("state",new{stage,items=d.Items.Select(v=>new{id=v.Data.Identifier.Id,reference=Ref(v)}).ToArray(),cursor=d.Cursor,
            undo=snapshotItems?.Select(v=>new{id=v.Data.Identifier.Id,reference=Ref(v)}).ToArray(),undoCursor=snap?.GetType().GetProperty("Offset")?.GetValue(snap),
            d.Updates,d.Changes,requests=requests.Count,loading=d.Loading(),refreshing=d.Refreshing(),canUndo=d.CanUndo(),
            d.Scroll.VerticalOffset,d.Scroll.ExtentHeight,d.Scroll.ViewportHeight});
    }
    static async Task Create(bool partition,string[] ids)
    {
        slot.Children.Clear();await Drain();
        // Release only obsolete outstanding external calls, after their controls are detached.
        foreach(var r in requests.Where(r=>!r.Released).ToArray())r.Release([],r.Offset);
        await Drain();requests.Clear();
        Offline48.Settings["PopularPageLastSelectedSectionId"]="Recommend";
        d=new();
        var service=GlobalDependencies.Kernel.GetRequiredService<IVideoDiscoveryService>();
        if(partition){
            var page=new VideoPartitionDetailViewModel(new PartitionViewModel(new("1","Controlled partition")){Children=[new(new("2","Controlled child"))]},service);
            d.Vm=page;d.Items=page.Videos;d.Refresh=page.RefreshCommand;d.Load=page.LoadVideosCommand;d.Undo=page.UndoRefreshCommand;
            d.Initialize=()=>page.InitializeCommand.ExecuteAsync(null);d.Switch=()=>page.ChangeChildPartitionCommand.ExecuteAsync(page.Children[1]);
            d.Loading=()=>page.IsVideoLoading;d.Refreshing=()=>page.IsRecommendRefreshing;d.CanUndo=()=>page.CanUndoRefresh;
            d.Body=new VideoPartitionMainBody{ViewModel=page};d.Header=new VideoPartitionMainHeader{ViewModel=page};page.VideoListUpdated+=(_,_)=>d.Updates++;
        }else{
            var page=new PopularPageViewModel(service,NullLogger<PopularPageViewModel>.Instance);
            d.Vm=page;d.Items=page.Videos;d.Refresh=page.RefreshVideoCommand;d.Load=page.LoadVideosCommand;d.Undo=page.UndoRefreshCommand;
            d.Initialize=()=>page.InitializeCommand.ExecuteAsync(null);d.Switch=()=>{page.SelectSectionCommand.Execute(page.Sections[1]);return Task.CompletedTask;};
            d.Loading=()=>page.IsVideoLoading;d.Refreshing=()=>page.IsRecommendRefreshing;d.CanUndo=()=>page.CanUndoRefresh;
            d.Body=new PopularMainBody{ViewModel=page};d.Header=new PopularMainHeader{ViewModel=page};page.VideoListUpdated+=(_,_)=>d.Updates++;
        }
        d.Items.CollectionChanged+=(_,e)=>{d.Changes++;Write("collection",new{action=e.Action.ToString(),e.NewStartingIndex,e.OldStartingIndex});};
        d.Increment=(IncrementVerticalScrollControl)d.Body.FindName("View");d.Scroll=(ScrollView)d.Increment.FindName("View");
        slot.RowDefinitions.Clear();slot.RowDefinitions.Add(new(){Height=GridLength.Auto});slot.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var init=d.Initialize();var first=await Next(0);first.Release(ids,41);await init;
        await Until(()=>!d.Loading(),"initial command completion before mount");
        slot.Children.Add(d.Header);Grid.SetRow(d.Body,1);slot.Children.Add(d.Body);
        await Until(()=>d.Body.IsLoaded&&d.Scroll.ExtentHeight>0,"initial populated layout");
        if(ids.Length>1){
            await Until(()=>d.Scroll.ExtentHeight-d.Scroll.ViewportHeight>800,"long-list geometry");
            // A real mount may have queued a check while the viewport was empty.
            // Complete that observed request, then drain the queue; do not guess a delay.
            for(var i=0;i<4;i++){
                foreach(var pending in requests.Where(r=>!r.Released).ToArray())pending.Release([],41);
                await Drain();
            }
            Require(!d.Loading()&&requests.All(r=>r.Released),"initial automatic request barrier");
        }
        Snapshot("initial");
    }
    static string[] Initial()=>Enumerable.Range(100,30).Select(x=>x.ToString()).Take(15).Concat(["105"]).Concat(Enumerable.Range(115,15).Select(x=>x.ToString())).ToArray();
    static async Task Refresh(bool partition,string[] ids,long cursor=73,bool fail=false)
    {
        var index=requests.Count;var task=d.Refresh.ExecuteAsync(null);var r=await Next(index);r.Release(ids,cursor,fail);
        if(!partition&&!fail&&ids.Length>0){var stop=await Next(index+1);stop.Release([],cursor);}
        await task;await Drain();
    }
    static async Task Pause(string stage)
    {
        Snapshot(stage);continueGate=new();status.Text="CAPTURE "+stage;
        await continueGate.Task.WaitAsync(TimeSpan.FromMinutes(4));status.Text="RUNNING";
    }
    static async Task Case(string name,Func<Task> test)
    {
        current=name;try{await test();results.Add(new{name,status="PASS"});}
        catch(Exception e){Snapshot("failure");results.Add(new{name,status="FAIL",detail=e.ToString()});}
        File.WriteAllText(Path.Combine(Folder,"controlled-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
    }
    static async Task Run()
    {
        status.Text="RUNNING";
        foreach(var partition in new[]{false,true}){
            var prefix=partition?"Partition":"Popular";
            await Case(prefix+"ExactSequenceReferencesCursorUndo",async()=>{
                await Create(partition,Initial());var original=d.Items.ToArray();
                // Middle position is real ScrollView state, never a realized-card count.
                d.Scroll.ScrollTo(0,600,new(ScrollingAnimationMode.Disabled));await Until(()=>Math.Abs(d.Scroll.VerticalOffset-600)<2,"middle scroll");
                await Refresh(partition,["101","900","900","901"]);
                var expected=new[]{"101","900","901"}.Concat(Initial().Where(x=>x!="101")).ToArray();
                Require(d.Items.Select(v=>v.Data.Identifier.Id).SequenceEqual(expected),"full merged ID sequence");
                Require(original.Where(v=>v.Data.Identifier.Id!="101").All(v=>d.Items.Any(x=>ReferenceEquals(x,v))),"unchanged old references");
                Require(d.Cursor==73&&d.CanUndo(),"refresh cursor/undo");Snapshot("refreshed");
                d.Undo.Execute(null);Snapshot("undo-synchronous");
                Require(d.Items.SequenceEqual(original)&&d.Cursor==41&&!d.CanUndo(),"exact undo references/order/cursor");
                await Drain();
            });
            foreach(var fails in new[]{false,true})await Case(prefix+(fails?"Failed":"Empty")+"RefreshPreservesSnapshotAndManualRecovery",async()=>{
                await Create(partition,Initial());var original=d.Items.ToArray();await Refresh(partition,["900"]);
                var before=d.Items.ToArray();var changes=d.Changes;var updates=d.Updates;var count=requests.Count;
                await Refresh(partition,[],99,fails);Snapshot("empty-or-failed-refresh");
                Require(requests.Count==count+1&&d.Items.SequenceEqual(before)&&d.Cursor==73&&d.Changes==changes&&d.Updates==updates&&d.CanUndo(),"empty/failure mutated list/cursor/undo or repeated batch");
                d.Undo.Execute(null);Require(d.Items.SequenceEqual(original)&&d.Cursor==41,"previous snapshot lost");
                await Refresh(partition,["902"]);Require(d.Items[0].Data.Identifier.Id=="902"&&d.CanUndo(),"manual refresh did not recover");
                Snapshot("manual-recovered");
            });
            await Case(prefix+"OldReturnAfterSectionSwitch",async()=>{
                await Create(partition,Initial());var index=requests.Count;var oldTask=d.Refresh.ExecuteAsync(null);var old=await Next(index);
                var switched=d.Switch();var next=await Next(index+1);next.Release(["700"],17);await switched;await Until(()=>!d.Loading(),"new section load");
                var newItems=d.Items.ToArray();old.Release(["999"],999);await oldTask;await Drain();Snapshot("old-return-after-switch");
                Require(d.Items.SequenceEqual(newItems)&&d.Items[0].Data.Identifier.Id=="700"&&!d.CanUndo()&&!d.Refreshing(),"old refresh crossed section boundary");
            });
        }
        await Case("PopularOldAutomaticPageArrivesDuringRefresh",async()=>{
            await Create(false,Initial());var index=requests.Count;d.Scroll.ScrollTo(0,d.Scroll.ExtentHeight,new(ScrollingAnimationMode.Disabled));
            var old=await Next(index);Require(old.Offset==41,"old auto cursor");var task=d.Refresh.ExecuteAsync(null);var fresh=await Next(index+1);
            var before=d.Items.ToArray();old.Release(["999"],999);await Drain();Snapshot("old-auto-during-refresh");
            Require(d.Items.SequenceEqual(before)&&d.Refreshing()&&d.Cursor==41,"late old auto publication");
            fresh.Release(["900"],73);(await Next(index+2)).Release([],73);await task;Snapshot("refresh-after-stale-auto");
            Require(!d.Items.Any(x=>x.Data.Identifier.Id=="999")&&d.Cursor==73,"stale auto contaminated fresh list/cursor");
        });
        foreach(var fails in new[]{false,true})await Case((fails?"Failed":"Empty")+"AutomaticPageStopsAndManualLoadRecovers",async()=>{
            await Create(false,["100"]);var terminal=await Next(1);terminal.Release([],41,fails);await Until(()=>!d.Loading(),"terminal page completion");
            var count=requests.Count;var updates=d.Updates;Snapshot("terminal-completed");
            // Bounded no-loop observation, not readiness: four real 500ms production delay periods.
            var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.ElapsedMilliseconds<2000){await Drain();Require(requests.Count==count,"self-scheduled empty/failure loop");await Task.Delay(20);}
            Require(updates==d.Updates,"terminal page rescheduled list update");Write("quiet-window",new{milliseconds=watch.ElapsedMilliseconds,requests=count,updates});
            var manual=d.Load.ExecuteAsync(null);var recovery=await Next(count);Require(recovery.Offset==41,"recovery cursor");recovery.Release(["200"],42);await manual;await Drain();
            Require(d.Items.Select(v=>v.Data.Identifier.Id).SequenceEqual(new[]{"100","200"}),"manual load recovery");Snapshot("manual-load-recovered");
        });
        await Case("VisibleRefreshUndoThenAutomaticAppend",async()=>{
            await Create(false,Initial());var original=d.Items.ToArray();await Pause("before-visible-refresh");
            // Runner invokes the actual production header button. External reply waits here.
            var index=requests.Count;var req=await Next(index);req.Release(["900"],73);(await Next(index+1)).Release([],73);await Until(()=>!d.Refreshing()&&d.CanUndo(),"visible refresh finish");
            await Pause("after-visible-refresh");
            await Until(()=>!d.CanUndo(),"actual undo button input");Snapshot("visible-undo");Require(d.Items.SequenceEqual(original)&&d.Cursor==41,"visible undo exact state");
            await Pause("after-visible-undo");
            // Explicitly reach the bottom; automatic continuation uses restored cursor and may append duplicates by contract.
            index=requests.Count;d.Scroll.ScrollTo(0,d.Scroll.ExtentHeight,new(ScrollingAnimationMode.Disabled));var auto=await Next(index);
            Require(auto.Offset==41,"undo continuation cursor");auto.Release(["100","800"],42);await Until(()=>!d.Loading(),"auto append completion");
            Require(d.Items.Take(original.Length).SequenceEqual(original)&&d.Items.Skip(original.Length).Select(x=>x.Data.Identifier.Id).SequenceEqual(new[]{"100","800"}),"undo continuation append contract");Snapshot("undo-followed-by-auto-append");
        });
        status.Text=results.All(x=>JsonSerializer.Serialize(x).Contains("\"PASS\""))?"DONE PASS":"DONE FAIL";
    }
}
