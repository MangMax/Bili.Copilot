using BiliCopilot.UI;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.ViewModels.Components;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Richasy.BiliKernel.Bili.Search;
using System.Reflection;
namespace Acceptance51;
public partial class App:Application {
    Window window=null!; AppSearchBox control=null!; AutoSuggestBox box=null!; TextBox text=null!;
    StackPanel slot=new(); TextBlock status=new(){Text="starting",TextWrapping=TextWrapping.Wrap};
    Fixture fixture=null!; int fence; bool observing; bool renderPending; readonly Dictionary<string,Action> actions=[];
    Microsoft.UI.Dispatching.DispatcherQueueTimer? fenceTimer;
    public App(){InitializeComponent();UnhandledException+=(_,e)=>Trace.Write("unhandled",e.Exception.ToString());}
    protected override void OnLaunched(LaunchActivatedEventArgs args) {
        var service=DispatchProxy.Create<ISearchService,Fixture>();fixture=(Fixture)(object)service;
        GlobalDependencies.Model=new(service,new FixtureLogger<SearchBoxViewModel>());
        window=new Window{Title="Offline search acceptance 51 — cd7811f"};
        var root=new StackPanel{Spacing=16,Padding=new Thickness(24)};
        root.Children.Add(new TextBlock{Text="Search #51 · real control / offline service",Style=(Style)Resources["TitleTextBlockStyle"]});
        root.Children.Add(new TextBlock{Text="Type in the production search control. Responses are manually released.",TextWrapping=TextWrapping.Wrap});
        var commands=new WrapPanelReplacement();
        void Button(string id,Action action) {actions.Add(id,action);var b=new Button{Content=id};AutomationProperties.SetAutomationId(b,id);b.Click+=(_,_)=>{Trace.Write("action",id);action();};commands.Children.Add(b);}
        Button("ReleaseNewest",()=>fixture.Complete(false));Button("ReleaseOldest",()=>fixture.Complete(true));
        Button("Detach",()=>{slot.Children.Clear();Snapshot("detached");});
        Button("Attach",()=>{if(slot.Children.Count==0)slot.Children.Add(control);});
        Button("Fence",()=>{fenceTimer=window.DispatcherQueue.CreateTimer();fenceTimer.Interval=TimeSpan.FromMilliseconds(850);fenceTimer.IsRepeating=false;fenceTimer.Tick+=(_,_)=>{Snapshot("fence"+(++fence));};fenceTimer.Start();});
        Button("Region",()=>{GlobalDependencies.Model.SetExtraRegion("history","History fixture");Snapshot("region");});
        Button("Global",()=>GlobalDependencies.Model.SetExtraRegion("",""));
        Button("Close",()=>window.Close());
        root.Children.Add(commands);AutomationProperties.SetAutomationId(status,"Status");root.Children.Add(status);
        root.Children.Add(new TextBlock{Text="Search",Style=(Style)Resources["SubtitleTextBlockStyle"]});root.Children.Add(slot);
        control=new AppSearchBox();slot.Children.Add(control);
        control.Loaded+=(_,_)=>{
            box=(AutoSuggestBox)control.FindName("Box");AutomationProperties.SetAutomationId(box,"SearchBox");
            if(!observing){observing=true;
                box.TextChanged+=(_,e)=>{Trace.Write("text",new{box.Text,reason=e.Reason.ToString(),keyword=GlobalDependencies.Model.Keyword});Snapshot("text");};
                box.QuerySubmitted+=(_,e)=>{Trace.Write("submit",new{e.QueryText,chosen=e.ChosenSuggestion!=null});Snapshot("submit");};
                box.RegisterPropertyChangedCallback(AutoSuggestBox.IsSuggestionListOpenProperty,(_,_)=>Snapshot("popup"));
                GlobalDependencies.Model.Suggestion.CollectionChanged+=(_,_)=>Snapshot("collection");
                fixture.Changed+=()=>Snapshot("request");
                box.LayoutUpdated+=ObserveTextBox;
            }
            Snapshot("loaded");
        };
        control.Unloaded+=(_,_)=>Snapshot("unloaded");
        window.Content=root;window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1120,800));window.Activate();
        CompositionTarget.Rendering+=(_,_)=>{if(renderPending){renderPending=false;Trace.Write("render",new{loaded=control.IsLoaded,text=box?.Text,open=box?.IsSuggestionListOpen,items=GlobalDependencies.Model.Suggestion.Select(s=>s.SearchContent).ToArray()});}};
        Trace.Write("launch",new{pid=Environment.ProcessId,identity=Windows.ApplicationModel.Package.Current.Id.Name});
        _=Task.Run(async()=>{
            while(true){
                using var pipe=new System.IO.Pipes.NamedPipeServerStream("bili51-b48a",System.IO.Pipes.PipeDirection.In);
                await pipe.WaitForConnectionAsync();using var reader=new StreamReader(pipe);
                while(await reader.ReadLineAsync() is string command) window.DispatcherQueue.TryEnqueue(()=>{Trace.Write("pipe",command);if(actions.TryGetValue(command,out var action))action();else Snapshot(command);});
            }
        });
    }
    void ObserveTextBox(object? sender,object e) {
        if(text!=null)return;
        text=Descendants(box).OfType<TextBox>().FirstOrDefault()!;
        if(text==null)return;
        AutomationProperties.SetAutomationId(text,"SearchInput");
        text.TextCompositionStarted+=(_,_)=>Trace.Write("composition-start",new{text.Text});
        text.TextCompositionChanged+=(_,_)=>Trace.Write("composition-change",new{text.Text});
        text.TextCompositionEnded+=(_,_)=>Trace.Write("composition-end",new{text.Text});
        text.GotFocus+=(_,_)=>Snapshot("focus");text.LostFocus+=(_,_)=>Snapshot("blur");
        Trace.Write("input-ready");
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent){for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var c=VisualTreeHelper.GetChild(parent,i);yield return c;foreach(var d in Descendants(c))yield return d;}}
    void Snapshot(string condition) {
        var vm=GlobalDependencies.Model;
        var data=new{condition,loaded=control.IsLoaded,text=box?.Text,keyword=vm.Keyword,open=box?.IsSuggestionListOpen??false,focus=box?.FocusState.ToString(),items=vm.Suggestion.Select(s=>s.SearchContent).ToArray(),requests=fixture.Requests.Count};
        Trace.Write("state",data);
        renderPending=true;
        status.Text=$"{condition} | requests={data.requests} | open={data.open} | loaded={data.loaded} | text={data.text} | items={string.Join(",",data.items)}";
    }
}
// Only test command layout; no search behavior.
public sealed partial class WrapPanelReplacement:StackPanel {public WrapPanelReplacement(){Orientation=Orientation.Horizontal;Spacing=4;}}
