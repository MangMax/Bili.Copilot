using Richasy.Danmaku.Legacy;
using Richasy.Danmaku.Models;
using Richasy.Danmaku.Enums;
using Pool = Windows.System.Threading.ThreadPool;
static List<DanmakuItem> List(string id)=>new(){new(){Id=id,StartMs=1,Mode=DanmakuMode.Rolling}};
static void Await(Func<bool> ready,string message){if(!SpinWait.SpinUntil(ready,3000))throw new Exception("Timeout: "+message);}
static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
static void Reject(Action action){try{action();}catch(InvalidOperationException ex){Assert(ex.InnerException != null,"original cause missing");return;}throw new Exception("request accepted after terminal failure");}
static (DanmakuFrostMaster,DanmakuRender) New(){var m=new DanmakuFrostMaster(new());m.SetDanmakuEnabledType(true,true,true);m.SetDanmakuList(List("OLD"));Pool.Enter.Wait();return(m,DanmakuRender.Last);}
static void Finish(DanmakuFrostMaster m){m.Close();Pool.Go.Set();Assert(Pool.Worker.Join(3000),"worker leaked");}
int failures=0;
void Test(string name,Action action){try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);try{DanmakuRender.Last.OwnerClose?.Invoke();}catch{}}}
Test("ListReplacementLatestSeek",()=>{
 var(m,r)=New();r.OwnerClose=()=>Finish(m);var old=m.GetDanmakuList();
 lock(old){m.Seek(100);Pool.Go.Set();Await(()=>Pool.Worker.ThreadState.HasFlag(ThreadState.WaitSleepJoin),"snapshot blocked");m.SetDanmakuList(List("NEW"));m.Seek(200);}
 Await(()=>r.Starts>0,"ready");Assert(r.Items.SequenceEqual(new[]{"NEW"}),"expected NEW; actual "+string.Join(",",r.Items));Finish(m);
});
foreach(var op in new[]{"Stop","Pause","Item","Start"}) Test("FailureThenLatestSeek_"+op,()=>{
 var(m,r)=New();r.OwnerClose=()=>Finish(m);r.FailOperation=op;m.Seek(100);Pool.Go.Set();Await(()=>!Pool.Worker.IsAlive,"faulted worker exit");
 Reject(()=>m.Seek(200));Reject(()=>m.UpdateTime(200));Reject(()=>m.Resume());Reject(()=>m.SetDanmakuList(List("NEW")));Assert(r.Starts==0,"fault reported ready");Finish(m);
});
Test("ReplacementBeforeResourcesReady",()=>{var(m,r)=New();r.OwnerClose=()=>Finish(m);r.ResourcesReady=false;m.Seek(100);Pool.Go.Set();m.SetDanmakuList(List("NEW"));m.Seek(200);r.ResourcesReady=true;r.Wake();Await(()=>r.Starts==1,"ready");Assert(r.Items.Single()=="NEW","stale scene");Finish(m);});
foreach(var op in new[]{"Clear","Stop","Close"}) Test("RetirementWhileSnapshotBlocked_"+op,()=>{
 var(m,r)=New();r.OwnerClose=()=>Finish(m);var old=m.GetDanmakuList();lock(old){m.Seek(100);Pool.Go.Set();Await(()=>Pool.Worker.ThreadState.HasFlag(ThreadState.WaitSleepJoin),"snapshot blocked");if(op=="Clear")m.Clear();else if(op=="Stop")m.Stop();else m.Close();}
 if(op!="Close"){m.SetDanmakuList(List("NEW"));m.Seek(200);Await(()=>r.Starts==1,"ready");Assert(r.Items.Single()=="NEW","stale item");}Finish(m);Assert(r.Starts==(op=="Close"?0:1),"retired request committed");
});
Test("PauseLatestSeek",()=>{var(m,r)=New();r.OwnerClose=()=>Finish(m);m.Seek(100);m.Pause();m.Seek(200);Pool.Go.Set();Await(()=>r.Starts==1,"ready");Assert(r.LastPaused,"pause lost");Finish(m);});
Test("InvalidRequestDoesNotFaultWorker",()=>{var(m,r)=New();r.OwnerClose=()=>Finish(m);try{m.AddRealtimeDanmaku(null,false);throw new Exception("null accepted");}catch(ArgumentNullException){}try{m.AddRealtimeDanmaku(List("BAD")[0],false,99);throw new Exception("layer accepted");}catch(ArgumentOutOfRangeException){}m.Seek(200);Pool.Go.Set();Await(()=>r.Starts==1,"worker recovered");Assert(m.GetType().GetProperty("WorkerFailure")?.GetValue(m)==null,"validation faulted worker");Finish(m);});
Test("NewSeekCancelsBetweenItems",()=>{
 var(m,r)=New();r.OwnerClose=()=>Finish(m);var many=Enumerable.Range(0,1000).Select(i=>new DanmakuItem{Id="ITEM"+i,StartMs=(uint)(i+1),Mode=DanmakuMode.Rolling}).ToList();m.SetDanmakuList(many);
 using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();int calls=0;
 r.BeforeItem=()=>{if(Interlocked.Increment(ref calls)==1){entered.Set();if(!release.Wait(3000))throw new Exception("gate timeout");}};
 m.Seek(1000);Pool.Go.Set();Assert(entered.Wait(3000),"first item not reached");var watch=System.Diagnostics.Stopwatch.StartNew();m.Seek(2);Assert(watch.ElapsedMilliseconds<100,"seek waits for resource preparation");release.Set();Await(()=>r.Starts==1,"latest ready");Assert(calls==3&&r.Items.SequenceEqual(new[]{"ITEM0","ITEM1"}),"superseded prefix not cancelled");Finish(m);
});
Test("FailureRetiresAlreadyQueuedLatestSeek",()=>{
 var(m,r)=New();r.OwnerClose=()=>Finish(m);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();r.BeforeItem=()=>{entered.Set();if(!release.Wait(3000))throw new Exception("gate timeout");};r.FailOperation="Item";
 m.Seek(100);Pool.Go.Set();Assert(entered.Wait(3000),"item not reached");m.Seek(200);release.Set();Await(()=>!Pool.Worker.IsAlive,"terminal exit");Assert(m.GetType().GetProperty("WorkerFailure")?.GetValue(m)!=null&&r.Starts==0,"failed request marked ready");Reject(()=>m.Seek(300));Finish(m);
});
Console.WriteLine($"Failed: {failures}");return failures==0?0:1;
