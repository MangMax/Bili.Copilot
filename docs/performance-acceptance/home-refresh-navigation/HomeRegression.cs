using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using RichasyKernel;
using Richasy.BiliKernel.Bili.Media;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.Controls.Settings;
using BiliCopilot.UI.Controls.VideoPartition;
using BiliCopilot.UI.Controls.Popular;
using BiliCopilot.UI.Pages;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.ViewModels.View;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Toolkits;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
namespace BiliCopilot.UI;
internal static class ControlledRefresh
{
 static Window host=null!; static BiliCopilot.UI.Controls.MainFrame frame=new(); static Grid slot=new(); static StackPanel actions=new(){Orientation=Orientation.Horizontal}; static TextBlock status=new();
 static int serial; static string batch="initial"; static List<HomeResult> results=[];
 static TaskCompletionSource<IReadOnlyList<Partition>> directory=new();
 static int directoryCalls,videoCalls,partitionInitialRequests; static TaskCompletionSource<(IReadOnlyList<VideoInformation>,long)>? pendingMore;
 static string Folder=>Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.Path;
 static void Log(string kind,object data)=>File.AppendAllText(Path.Combine(Folder,"controlled.jsonl"),JsonSerializer.Serialize(new HomeResult(kind,"TRACE",data.ToString()!),HomeJson.Default.HomeResult)+Environment.NewLine);
 public static IKernelBuilder WithHomeDiscovery(this IKernelBuilder builder){builder.Services.AddSingleton<IVideoDiscoveryService,HomeDiscovery>();return builder;}
 internal static Task<IReadOnlyList<Partition>> Directory(){directoryCalls++;Log("directory",directoryCalls);return directory.Task;}
 internal static async Task<(IReadOnlyList<VideoInformation>,long)> Videos(string method,long offset){
  if(method=="Partition" && offset==0 && batch=="initial")partitionInitialRequests++;
  videoCalls++;Log("request",new{method,offset,batch});
  if(offset==1 && pendingMore!=null)return await pendingMore.Task;
  IReadOnlyList<VideoInformation> videos=offset==0?Enumerable.Range(0,60).Select(i=>new VideoInformation(new(batch+"-"+i,batch+" item "+i,null),null,120,"BV"+batch+i)).ToArray():[];
  await Task.Delay(350);return (videos,1L);
 }
 static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject {if(root is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Find<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
 static void Check(string name,bool pass,object data){results.Add(new HomeResult(name,pass?"PASS":"FAIL",data.ToString()!));Log(name,new{pass,data});}
 static void Visible(string name,FrameworkElement body,System.Collections.ObjectModel.ObservableCollection<BiliCopilot.UI.ViewModels.Items.VideoItemViewModel> items){
  var repeater=Find<ItemsRepeater>(body).Single();var scroll=Find<ScrollView>(body).Single();var rows=new List<string>();int count=0;bool valid=true;
  for(int i=0;i<items.Count;i++)if(repeater.TryGetElement(i) is FrameworkElement root){
   var rect=root.TransformToVisual(scroll).TransformBounds(new Windows.Foundation.Rect(0,0,root.ActualWidth,root.ActualHeight));
   if(rect.Bottom<=0 || rect.Top>=scroll.ActualHeight || rect.Right<=0 || rect.Left>=scroll.ActualWidth)continue;
   count++;var card=Find<VideoCardControl>(root).SingleOrDefault();var expected=items[i];var context=root.DataContext as BiliCopilot.UI.ViewModels.Items.VideoItemViewModel;var content=(root as ContentPresenter)?.Content as BiliCopilot.UI.ViewModels.Items.VideoItemViewModel;
   bool title=card!=null && Find<Richasy.WinUIKernel.Share.Base.TrimTextBlock>(card).Any(x=>x.Text==expected.Title);
   bool match=card!=null && ReferenceEquals(card.ViewModel,expected) && ReferenceEquals(context,expected) && ReferenceEquals(content,expected) && title;valid &=match;
   rows.Add($"i={i} expected={expected.Data.Identifier.Id} context={context?.Data.Identifier.Id} content={content?.Data.Identifier.Id} card={card?.ViewModel?.Data.Identifier.Id} rect={rect} title={title} match={match}");
  }
  Check(name,count>0 && valid,string.Join(" | ",rows));
 }
 static async Task Settle()=>await Task.Delay(1300);
 public static void Start(){
  host=new(){Title="Owned home regression"};var root=new Grid();root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new());
  var run=new Button{Content="Run"};AutomationProperties.SetAutomationId(run,"RunControlled");run.Click+=async(_,_)=>{run.IsEnabled=false;await Run();};actions.Children.Add(run);
  var close=new Button{Content="Close"};AutomationProperties.SetAutomationId(close,"CloseOwned");close.Click+=(_,_)=>host.Close();actions.Children.Add(close);AutomationProperties.SetAutomationId(status,"ControlledStatus");status.Text="READY";actions.Children.Add(status);root.Children.Add(actions);Grid.SetRow(slot,1);slot.Children.Add(frame);root.Children.Add(slot);host.Content=root;host.AppWindow.Resize(new(1600,1100));host.Activate();
 }
 static async Task Run(){try{
  SettingsToolkit.WriteLocalSetting(SettingNames.VideoPartitionPageLastSelectedPartitionId,"13");
  frame.NavigateTo(typeof(VideoPartitionPage));await Settle();
  var vm=GlobalDependencies.Kernel.GetRequiredService<VideoPartitionPageViewModel>();
  Check("NavigationStartsDirectory",directoryCalls==1,new{directoryCalls,videoCalls});
  frame.NavigateTo(typeof(PopularPage));await Settle();
  var earlyPopular=GlobalDependencies.Kernel.GetRequiredService<PopularPageViewModel>();
  Check("PopularLoadsBeforeSlowDirectory",!directory.Task.IsCompleted && earlyPopular.Videos.Count==60,new{count=earlyPopular.Videos.Count,directoryCalls});
  frame.NavigateTo(typeof(VideoPartitionPage));await Settle();
  directory.SetResult([new Partition("13","Anime",children:[new Partition("24","Child")]),new Partition("1","Other",children:[])]);await Settle();
  var body=Find<VideoPartitionMainBody>(frame).Single();
  Check("FirstNavigationRequestsAndDisplays",partitionInitialRequests==1 && vm.SelectedPartition?.Videos.Count==60 && ReferenceEquals(body.ViewModel,vm.SelectedPartition),new{videoCalls,count=vm.SelectedPartition?.Videos.Count,bodySame=ReferenceEquals(body.ViewModel,vm.SelectedPartition)});
  Visible("InitialVisibleCards",body,vm.SelectedPartition!.Videos);
  var saved=vm.SelectedPartition;frame.NavigateTo(typeof(PopularPage));await Settle();frame.NavigateTo(typeof(VideoPartitionPage));await Settle();
  Check("ReturnKeepsContent",vm.SelectedPartition?.Videos.Count==60,new{videoCalls,count=vm.SelectedPartition?.Videos.Count});
  slot.Children.Clear();await Settle();slot.Children.Add(frame);await Settle();
  Check("RepeatedLoadedDoesNotDuplicateFirstRequest",partitionInitialRequests==1 && vm.SelectedPartition!.Videos.Count==60,new{partitionInitialRequests,count=vm.SelectedPartition!.Videos.Count});
  Visible("ReturnedVisibleCards",body,vm.SelectedPartition!.Videos);
  // Actual settings control TwoWay binding, production settings VM and persisted toolkit.
  var settings=new IncrementalRecommendRefreshSettingControl();actions.Children.Add(settings);await Settle();var toggle=Find<ToggleSwitch>(settings).Single();
  var svm=GlobalDependencies.Kernel.GetRequiredService<SettingsPageViewModel>();
  foreach(bool value in new[]{true,false,true,false}){
   toggle.IsOn=value;await Settle();batch="batch"+(++serial);
   bool persisted=SettingsToolkit.ReadLocalSetting(SettingNames.IncrementalRecommendRefresh,!value);
   var fresh=new SettingsToolkit();bool reread=((ISettingsToolkit)fresh).ReadLocalSetting("IncrementalRecommendRefresh",!value);
   var old=saved!.Videos.Select(v=>v.Data.Identifier.Id).ToArray();await saved.RefreshCommand.ExecuteAsync(null);await Settle();
   var ids=saved.Videos.Select(v=>v.Data.Identifier.Id).ToArray();
   Visible("VisiblePartitionToggle"+serial,body,saved.Videos);
   Check("PartitionToggle"+serial,persisted==value && reread==value && svm.IncrementalRecommendRefresh==value && ids.Length==(value?old.Length+60:60) && (value || !ids.Intersect(old).Any()),new{value,persisted,reread,svm=svm.IncrementalRecommendRefresh,oldCount=old.Length,count=ids.Length,first=ids.FirstOrDefault()});
  }
  foreach(bool value in new[]{false,true}){
   toggle.IsOn=value;await Settle();var old=saved!.Videos.ToArray();batch="pending"+(++serial);pendingMore=new();
   var loading=saved.LoadVideosCommand.ExecuteAsync(null);
   var header=Find<VideoPartitionMainHeader>(frame).Single();var button=Find<Button>(header).Single(b=>AutomationProperties.GetAutomationId(b)=="RefreshRecommendButton");
   Check("PendingRefreshButtonEnabled"+value,button.Command?.CanExecute(null)==true,saved.IsVideoLoading);
   button.Command!.Execute(null);if(saved.RefreshCommand.ExecutionTask is {} refresh)await refresh;
   pendingMore.SetResult(([new VideoInformation(new("stale-page","stale",null),null,120,"BVstale")],99));await loading;pendingMore=null;await Settle();
   var ids=saved.Videos.Select(v=>v.Data.Identifier.Id).ToArray();
   Check("PendingPaginationRespectsSetting"+value,ids.Length==(value?old.Length+60:60) && !ids.Contains("stale-page") && ids[0]==batch+"-0",new{value,old=old.Length,count=ids.Length,first=ids.FirstOrDefault(),stale=ids.Contains("stale-page")});
   Visible("PendingVisible"+value,body,saved.Videos);
  }
  SettingsToolkit.WriteLocalSetting(SettingNames.PopularPageLastSelectedSectionId,"Recommend");frame.NavigateTo(typeof(PopularPage));await Settle();var pop=GlobalDependencies.Kernel.GetRequiredService<PopularPageViewModel>();Check("PopularFirstNavigation",pop.Videos.Count==60,new{count=pop.Videos.Count});Visible("PopularVisibleFirst",Find<PopularMainBody>(frame).Single(),pop.Videos);
  foreach(bool value in new[]{true,false}){svm.IncrementalRecommendRefresh=value;batch="batch"+(++serial);var old=pop.Videos.Count;await pop.RefreshVideoCommand.ExecuteAsync(null);await Settle();Check("PopularToggle"+serial,pop.Videos.Count==(value?old+60:60),new{value,count=pop.Videos.Count,old});Visible("PopularVisibleToggle"+serial,Find<PopularMainBody>(frame).Single(),pop.Videos);}
 }catch(Exception e){Check("Exception",false,e.ToString());}finally{File.WriteAllText(Path.Combine(Folder,"controlled-results.json"),JsonSerializer.Serialize(results,HomeJson.Default.ListHomeResult));status.Text="DONE";}}
}

internal sealed record HomeResult(string name,string status,string data);
[JsonSerializable(typeof(HomeResult))]
[JsonSerializable(typeof(List<HomeResult>))]
internal partial class HomeJson : JsonSerializerContext {}
internal sealed class HomeDiscovery : IVideoDiscoveryService {
 public Task<IReadOnlyList<Partition>> GetVideoPartitionsAsync(CancellationToken cancellationToken=default)=>ControlledRefresh.Directory();
 public Task<(IReadOnlyList<VideoInformation> Items,long Offset)> GetRecommendVideoListAsync(long offset=0,int pageSize=0,CancellationToken cancellationToken=default)=>ControlledRefresh.Videos("Popular",offset);
 public Task<(IReadOnlyList<VideoInformation> Items,long Offset)> GetHotVideoListAsync(long offset=0,CancellationToken cancellationToken=default)=>ControlledRefresh.Videos("Hot",offset);
 public Task<(IReadOnlyList<VideoInformation> Videos,long Offset)> GetPartitionRecommendVideoListAsync(Partition partition,long offset=0,CancellationToken cancellationToken=default)=>ControlledRefresh.Videos("Partition",offset);
 public async Task<(IReadOnlyList<VideoInformation> Videos,long Offset,int NextPageNumber)> GetChildPartitionVideoListAsync(Partition childPartition,long offset=0,int pageNumber=0,PartitionVideoSortType sortType=PartitionVideoSortType.Default,CancellationToken cancellationToken=default){var(v,o)=await ControlledRefresh.Videos("Child",offset);return(v,o,1);}
 public Task<IReadOnlyList<VideoInformation>> GetPartitionRankingListAsync(Partition partition,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<VideoInformation>>([]);
 public Task<IReadOnlyList<VideoInformation>> GetCuratedPlaylistAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<VideoInformation>>([]);
 public Task<IReadOnlyList<VideoInformation>> GetGlobalRankingListAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<VideoInformation>>([]);
}
