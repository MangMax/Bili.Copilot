using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls.Primitives;
using BiliCopilot.UI.Controls.AI;
using BiliCopilot.UI.Controls.Markdown;
using BiliCopilot.UI.ViewModels.Core;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Appearance;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.ApplicationModel.DataTransfer;
namespace Acceptance46;

public partial class App:Application {
 public static string Folder=>Windows.Storage.ApplicationData.Current.LocalFolder.Path;
 static readonly object logLock=new();
 public static void Log(string kind,object value){lock(logLock)File.AppendAllText(Path.Combine(Folder,"events.jsonl"),JsonSerializer.Serialize(new{at=DateTimeOffset.UtcNow,kind,value})+"\n");}
 Window window; Grid root; AIPanel panel; AIViewModel vm; TextBlock status; DispatcherQueue queue; TaskCompletionSource next;
 public App(){InitializeComponent();UnhandledException+=(_,e)=>{Log("unhandled",e.Exception.ToString());};}
 protected override async void OnLaunched(LaunchActivatedEventArgs args){
  try {
   Services.Initialize();queue=DispatcherQueue.GetForCurrentThread();
   vm=new(NullLogger<AIViewModel>.Instance,queue);
   Services.Map[typeof(AIViewModel)]=vm;Services.Map[typeof(DispatcherQueue)]=queue;
   root=new Grid{Background=(Brush)Resources["ApplicationPageBackgroundThemeBrush"]};
   root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
   var toolbar=new StackPanel{Spacing=8,Margin=new Thickness(12)};root.Children.Add(toolbar);
   status=new TextBlock{Text="Starting",TextWrapping=TextWrapping.Wrap};AutomationProperties.SetAutomationId(status,"Status");toolbar.Children.Add(status);
   var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};toolbar.Children.Add(buttons);
   var advance=new Button{Content="Continue"};AutomationProperties.SetAutomationId(advance,"Continue");advance.Click+=(_,_)=>next?.TrySetResult();buttons.Children.Add(advance);
   var run=new Button{Content="Run production VM matrix"};AutomationProperties.SetAutomationId(run,"Run");run.Click+=async(_,_)=>{run.IsEnabled=false;await Run();};buttons.Children.Add(run);
   panel=new AIPanel{ViewModel=vm};Grid.SetRow(panel,1);root.Children.Add(panel);
   window=new Window{Title="Offline AIViewModel 46 8B8F",Content=root};window.AppWindow.Resize(new(850,900));window.Activate();
   await vm.InitializeCommand.ExecuteAsync(null);await vm.SelectServiceCommand.ExecutionTask;
   await Source(false);await Layout(()=>panel.IsLoaded && panel.ActualHeight>0);
   NameButtons();status.Text="READY";
   Log("ready",new{pid=Environment.ProcessId,identity=Windows.ApplicationModel.Package.Current.Id.Name,assembly=typeof(AIViewModel).Assembly.Location,model=vm.SelectedModel?.Id});
  }catch(Exception ex){Log("startup-failed",ex.ToString());if(status!=null)status.Text="FAIL startup "+ex.Message;}
 }
 static ArticleDetail Article(string title="Offline Article β")=>new(new("article-fixture",title,"Fixed offline article",null),null,null,"<p>文章内容 🧪 <strong>Markdown</strong></p>",DateTimeOffset.UnixEpoch,[],false,false);
 sealed class VoidObserver(SynchronizationContext original):SynchronizationContext {
  public TaskCompletionSource Done=new(TaskCreationOptions.RunContinuationsAsynchronously);
  public override void OperationCompleted()=>Done.TrySetResult();
  public override void Post(SendOrPostCallback callback,object state)=>original.Post(callback,state);
 }
 Task ObserveInjection(Action action){var original=SynchronizationContext.Current;var observer=new VoidObserver(original);SynchronizationContext.SetSynchronizationContext(observer);try{action();}finally{SynchronizationContext.SetSynchronizationContext(original);}return observer.Done.Task.WaitAsync(TimeSpan.FromSeconds(10));}
 async Task Source(bool video){
  await ObserveInjection(()=>{if(video){var id=new MediaIdentifier("video-fixture","Offline Video α",new BiliImage((Uri)null));vm.InjectVideoAsync(new(new VideoInformation(id,null),null),new(new("part-fixture","Offline part",null),1,4));}
  else vm.InjectArticleAsync(Article());});
  await Drain();NameButtons();
 }
 Task Changed(Func<bool> condition){
  if(condition())return Task.CompletedTask;
  var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  System.ComponentModel.PropertyChangedEventHandler h=null;h=(_,_)=>{if(condition())t.TrySetResult();};vm.PropertyChanged+=h;
  return Wait();async Task Wait(){try{if(condition())return;await t.Task.WaitAsync(TimeSpan.FromSeconds(10));}finally{vm.PropertyChanged-=h;}}
 }
 Task Drain(){var t=new TaskCompletionSource();queue.TryEnqueue(DispatcherQueuePriority.Low,()=>t.SetResult());return t.Task;}
 async Task Layout(Func<bool> condition){
  var t=new TaskCompletionSource();EventHandler<object> h=(_,_)=>{if(condition())t.TrySetResult();};root.LayoutUpdated+=h;
  try{root.UpdateLayout();if(condition())return;await t.Task.WaitAsync(TimeSpan.FromSeconds(10));}finally{root.LayoutUpdated-=h;}
 }
 static IEnumerable<DependencyObject> Tree(DependencyObject parent){yield return parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)foreach(var c in Tree(VisualTreeHelper.GetChild(parent,i)))yield return c;}
 void NameButtons(){foreach(var box in Tree(panel).OfType<ComboBox>())AutomationProperties.SetAutomationId(box,"ProductionModel");foreach(var b in Tree(panel).OfType<Button>()){
  var id=ReferenceEquals(b.Command,vm.CopyAnswerCommand)?"ProductionCopy":ReferenceEquals(b.Command,vm.CancelCommand)?"ProductionCancel":ReferenceEquals(b.Command,vm.DiscardCommand)?"ProductionDiscard":ReferenceEquals(b.Command,vm.RegenerateCommand)?"ProductionRegenerate":null;
  if(id!=null)AutomationProperties.SetAutomationId(b,id);
 }}
 void Assert(string name,bool ok,object detail=null){Log("assert",new{name,pass=ok,detail,state=new{vm.SourceTitle,vm.RequestText,vm.TempResult,vm.FinalResult,vm.ProgressTip,vm.IsGenerating,vm.ErrorMessage}});if(!ok)throw new Exception(name);}
 async Task Checkpoint(string name){NameButtons();await Layout(()=>panel.IsLoaded && panel.ActualWidth>0);next=new(TaskCreationOptions.RunContinuationsAsynchronously);status.Text="CAPTURE "+name;Log("checkpoint",new{name,structure=Tree(panel).OfType<FrameworkElement>().Select(e=>new{type=e.GetType().Name,e.ActualWidth,e.ActualHeight,e.Visibility,text=(e as TextBlock)?.Text,markdown=(e as MarkdownTextBlock)?.Text}).ToArray()});await next.Task.WaitAsync(TimeSpan.FromMinutes(3));}
 const string Answer="# 离线验收 🧪\n\n**粗体** café é\n\n|列|值|\n|---|---|\n|甲|乙|\n\n```csharp\nvar text = \"中文🧪\";\n```\n\n[离线链接](https://offline.invalid/)\n\nFINAL-Ω";
 async Task Case(string name,Func<Task> test){try{await test();Log("case",new{name,pass=true});}catch(Exception ex){Log("case",new{name,pass=false,error=ex.ToString()});status.Text="FAIL "+name;}}
 async Task Run(){
  await Case("AllSixProductionGenerationBranches",async()=>{
   foreach(bool video in new[]{false,true})foreach(int branch in new[]{0,1,2}){
    await Source(video);var p=Services.Plan();var task=branch==2?vm.SendQuestionCommand.ExecuteAsync("固定提问 🧪"):vm.QuickItems[branch].ExecuteCommand.ExecuteAsync(null);
    await p.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));await p.Send("# 离线验收 🧪\n\n");await Changed(()=>!string.IsNullOrEmpty(vm.TempResult));
    Assert($"{video}-{branch}-Streaming",vm.IsGenerating&&string.IsNullOrEmpty(vm.FinalResult));
    if(!video&&branch==0)await Checkpoint("article-streaming");
    await p.Send(Answer["# 离线验收 🧪\n\n".Length..]);p.Finish();await task.WaitAsync(TimeSpan.FromSeconds(10));
    Assert($"{video}-{branch}-Final",vm.FinalResult==Answer&&vm.TempResult==""&&!vm.IsGenerating&&string.IsNullOrEmpty(vm.ProgressTip));
    var prompt=Services.Prompts.Last();Assert($"{video}-{branch}-Preparation",prompt.Contains(video?"固定字幕":"文章内容")&&(branch!=1||prompt.Contains("固定热门评论")));
    await Layout(()=>Tree(panel).OfType<MarkdownTextBlock>().Any(m=>m.Text==Answer&&m.ActualHeight>0));
    if(!video&&branch==0){await Checkpoint("article-final-copy");Assert("RealCopyExactUnicodeMarkdown",await Clipboard.GetContent().GetTextAsync()==Answer);Log("clipboard-owned",new{text=Answer});}
    if(video&&branch==1)await Checkpoint("video-final");
   }
  });
  await Case("CancelledLateChunkAndException",async()=>{
   foreach(bool fail in new[]{false,true}){
    await Source(false);var old=Services.Plan();var oldTask=vm.SendQuestionCommand.ExecuteAsync("old");await old.Entered.Task;
    vm.CancelCommand.Execute(null);
    var current=Services.Plan();var currentTask=vm.SendQuestionCommand.ExecuteAsync("current");await current.Entered.Task;await current.Send("CURRENT");await Changed(()=>vm.TempResult=="CURRENT");var progress=vm.ProgressTip;
    if(fail)old.Fail();else old.Late("STALE");await old.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));await oldTask;
    Assert("LateOldCannotPublishOrClearCurrent-"+fail,vm.IsGenerating&&vm.TempResult=="CURRENT"&&vm.ProgressTip==progress&&string.IsNullOrEmpty(vm.ErrorMessage));
    current.Finish();await currentTask;Assert("CurrentCompletes-"+fail,vm.FinalResult=="CURRENT");
   }
  });
  await Case("SourceSwitchDuringPreparation",async()=>{
   foreach(string boundary in new[]{"GetSubtitleMetasAsync","GetSubtitleDetailAsync","GetCommentsAsync","GetServiceConfigAsync"}){
    await Source(boundary.Contains("Subtitle"));var g=Services.Hold(boundary);var oldTask=vm.QuickItems[boundary=="GetCommentsAsync"?1:0].ExecuteCommand.ExecuteAsync(null);await g.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
    await Source(false);var p=Services.Plan();var current=vm.SendQuestionCommand.ExecuteAsync("new source");await p.Entered.Task;await p.Send("NEW-SOURCE");await Changed(()=>vm.TempResult=="NEW-SOURCE");var progress=vm.ProgressTip;
    g.Release.SetResult();await oldTask.WaitAsync(TimeSpan.FromSeconds(10));Assert("StalePreparation-"+boundary,vm.IsGenerating&&vm.TempResult=="NEW-SOURCE"&&vm.ProgressTip==progress&&string.IsNullOrEmpty(vm.ErrorMessage));p.Finish();await current;
   }
  });
  await Case("DelayedPromptSourceSwitch",async()=>{
   var gate=Services.Hold("ReadLocalDataAsync");var injection=ObserveInjection(()=>vm.InjectArticleAsync(Article("Old pending prompts")));await gate.Entered.Task;
   await Source(true);var items=vm.QuickItems;gate.Release.SetResult();await injection;
   Assert("OldPromptCannotReplaceVideoPrompts",ReferenceEquals(items,vm.QuickItems)&&vm.SourceTitle=="Offline Video α");
  });
  await Case("RegenerateOldRequestLateCompletion",async()=>{
   await Source(false);var old=Services.Plan();var oldTask=vm.SendQuestionCommand.ExecuteAsync("regenerate this");await old.Entered.Task;
   var current=Services.Plan();vm.RegenerateCommand.Execute(null);var currentTask=vm.SendQuestionCommand.ExecutionTask;await current.Entered.Task;
   old.Fail();await old.Disposed.Task;await oldTask;Assert("RegenerationOwnsState",vm.IsGenerating&&vm.RequestText=="regenerate this"&&string.IsNullOrEmpty(vm.ErrorMessage));
   await current.Send("REGENERATED");current.Finish();await currentTask;Assert("RegeneratedAnswer",vm.FinalResult=="REGENERATED");
  });
  foreach(string cmd in new[]{"Cancel","Discard","Clear"})await Case("StopDiscardClear-"+cmd,async()=>{
    await Source(false);if(vm.SelectedModel==null){await vm.InitializeCommand.ExecuteAsync(null);await vm.SelectServiceCommand.ExecutionTask;}
    var p=Services.Plan();var task=vm.SendQuestionCommand.ExecuteAsync(cmd);await p.Entered.Task;await p.Send("pending");await Changed(()=>vm.TempResult=="pending");
    if(cmd=="Cancel")vm.CancelCommand.Execute(null);else if(cmd=="Discard")vm.DiscardCommand.Execute(null);else vm.ClearCommand.Execute(null);
    p.Fail();await p.Disposed.Task;await task;await Drain();Assert(cmd+"ClearsAndRejectsLate",!vm.IsGenerating&&vm.TempResult==""&&vm.FinalResult==""&&string.IsNullOrEmpty(vm.ErrorMessage));
    Assert(cmd+"ClearsProgress",string.IsNullOrEmpty(vm.ProgressTip));
  });
  await Case("NonStreaming",async()=>{
   if(vm.SelectedModel==null){await vm.InitializeCommand.ExecuteAsync(null);await vm.SelectServiceCommand.ExecutionTask;}await Source(false);
   BiliCopilot.UI.Toolkits.SettingsToolkit.Streaming=false;try{var p=Services.Plan();p.NonStreaming=Answer;var task=vm.SendQuestionCommand.ExecuteAsync("nonstreaming");await p.Entered.Task;p.End.SetResult();await task;Assert("NonStreamingExact",vm.FinalResult==Answer);}finally{BiliCopilot.UI.Toolkits.SettingsToolkit.Streaming=true;}
  });
  await Case("RealInputAndDispatcherDuringStreaming",async()=>{
   await Source(false);var p=Services.Plan();await Checkpoint("input-ready");await p.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));var task=vm.SendQuestionCommand.ExecutionTask;
   status.Text="STREAM-ACTIVE";
   var delays=new List<double>();for(int i=0;i<40;i++){var previous=vm.TempResult;await p.Send($"行{i} 🧪\n");await Changed(()=>vm.TempResult!=previous);await Task.Run(async()=>{var sent=Stopwatch.GetTimestamp();await Drain();lock(delays)delays.Add(Stopwatch.GetElapsedTime(sent).TotalMilliseconds);});}
   Log("dispatcher-proxy-ms",delays);await Checkpoint("input-streaming-stop");Assert("RealStopButton",!vm.IsGenerating);p.Fail();await p.Disposed.Task;await task;
  });
  // Read clipboard only after this run's real copy was verified. Never preserve prior clipboard.
  if(File.ReadAllText(Path.Combine(Folder,"events.jsonl")).Contains("clipboard-owned")){var data=Clipboard.GetContent();if(data.Contains(StandardDataFormats.Text)&&await data.GetTextAsync()==Answer){Clipboard.Clear();Log("clipboard-clear",true);}}
  status.Text="DONE";Log("done",true);
 }
}
