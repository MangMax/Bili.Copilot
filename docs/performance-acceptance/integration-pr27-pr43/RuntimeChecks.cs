using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using System.Reflection;
using System.Text.Json;
namespace Acceptance5053;

public partial class App
{
    async Task RunIntegrationChecks()
    {
        var results = new List<object>();
        async Task Check(string name, Func<Task> run)
        {
            try { await run(); results.Add(new { name, status="PASS", detail="" }); }
            catch(Exception ex) { results.Add(new { name, status="FAIL", detail=ex.ToString() }); }
        }
        void Require(bool ok, string reason) { if(!ok) throw new Exception(reason); }
        VideoInformation Video(string id) => new(default, null, bvId:id);
        (VideoPartitionDetailViewModel, PartitionFixture) PartitionPage()
        {
            var discovery=DispatchProxy.Create<IVideoDiscoveryService,PartitionFixture>();
            var f=(PartitionFixture)(object)discovery;
            var partition=new PartitionViewModel(new Partition("1","Root")) { Children=[new(new Partition("2","Child"))] };
            return (new VideoPartitionDetailViewModel(partition,discovery),f);
        }
        await Check("PartitionSwitchPreservesCacheOwnershipAndCursor", async () => {
            var (page,f)=PartitionPage(); await page.InitializeCommand.ExecuteAsync(null);
            var old=page.Videos.Single();
            await page.ChangeChildPartitionCommand.ExecuteAsync(page.Children[1]);
            Require(page.Videos.Single().Data.BvId=="child", "Child displayed recommendation cache");
            await page.ChangeChildPartitionCommand.ExecuteAsync(page.Children[0]);
            Require(ReferenceEquals(old,page.Videos.Single()),"Recommend identity lost or child cache leaked");
        });
        await Check("PartitionEmptyRefreshPreservesUndoAndStopsNotification", async () => {
            var (page,f)=PartitionPage(); await page.InitializeCommand.ExecuteAsync(null);
            var old=page.Videos.Single();
            f.Recommend=_=>Task.FromResult<(IReadOnlyList<VideoInformation>,long)>(([Video("fresh")],7));
            await page.RefreshCommand.ExecuteAsync(null);
            var fresh=page.Videos.Single(); var updates=0;page.VideoListUpdated+=(_,_)=>updates++;
            f.Recommend=_=>Task.FromResult<(IReadOnlyList<VideoInformation>,long)>(([],99));
            await page.RefreshCommand.ExecuteAsync(null);
            Require(page.Videos.Count==1 && ReferenceEquals(fresh,page.Videos[0]),"Empty refresh destroyed content");
            Require(updates==0,"Empty refresh scheduled loading");
            page.UndoRefreshCommand.Execute(null);
            Require(ReferenceEquals(old,page.Videos.Single()),"Previous snapshot lost");
        });
        await Check("PartitionLateRefreshDoesNotBlockChildOrPublishFailure", async () => {
            var (page,f)=PartitionPage(); await page.InitializeCommand.ExecuteAsync(null);
            var held=new TaskCompletionSource<(IReadOnlyList<VideoInformation>,long)>();f.Recommend=_=>held.Task;
            var refresh=page.RefreshCommand.ExecuteAsync(null);
            await page.ChangeChildPartitionCommand.ExecuteAsync(page.Children[1]);
            Require(page.Videos.Single().Data.BvId=="child","Pending old refresh blocked child");
            Require(page.RefreshCommand.CanExecute(null),"Old refresh disables current child refresh action");
            held.SetException(new IOException("late"));await refresh;
            Require(!page.CanUndoRefresh,"Old failure published into child");
        });
        await Check("RealControlsRefreshUndoAndRemount", async () => {
            New("Long");
            async Task Settle() { await Task.Delay(700); main.UpdateLayout(); }
            await Settle();
            Require(vm.Videos.Count==60,"Long initial page missing");
            var original=vm.Videos.ToArray();
            scroll.ScrollTo(0,600,new Microsoft.UI.Xaml.Controls.ScrollingScrollOptions(Microsoft.UI.Xaml.Controls.ScrollingAnimationMode.Disabled));
            await Settle();
            var before=scroll.VerticalOffset;
            SettingsToolkit.Incremental=true;
            service.Scenario="Fresh";service.Page=0;service.Batch=2;
            await vm.RefreshVideoCommand.ExecuteAsync(null);await Settle();
            Require(vm.Videos.Count==62,"Incremental refresh did not retain original page");
            Require(original.All(o=>vm.Videos.Any(v=>ReferenceEquals(v,o))),"Refresh lost identities");
            Snapshot("incremental-refreshed");
            vm.UndoRefreshCommand.Execute(null);await Settle();
            Require(vm.Videos.SequenceEqual(original),"Undo changed references/order");
            Trace.Write("anchor",new{before,afterUndo=scroll.VerticalOffset,delta=scroll.VerticalOffset-before});
            Require(Math.Abs(scroll.VerticalOffset-before)<2,"Undo scroll offset changed");
            slot.Children.Clear();await Settle();
            slot.Children.Add(side);slot.Children.Add(main);await Settle();
            Require(Subscribers("VideoListUpdated",typeof(BiliCopilot.UI.Controls.Popular.PopularMainBody))==1,"Remount subscription missing/duplicated");
            Snapshot("incremental-undone-remounted");
            SettingsToolkit.Incremental=false;
        });
        await Check("PopularHeaderUndoSurvivesRemount", async () => {
            New("Long"); await Task.Delay(800);
            var header = new BiliCopilot.UI.Controls.Popular.PopularMainHeader();
            Grid.SetColumn(header,1); slot.Children.Add(header); await Task.Delay(200);
            var undo=(Button)header.FindName("UndoRefreshButton");
            vm.CanUndoRefresh=true; await Task.Delay(250);
            Require(undo.Visibility==Visibility.Visible,"Initial undo is hidden");
            slot.Children.Remove(header); await Task.Delay(150);
            vm.CanUndoRefresh=false;
            slot.Children.Add(header); await Task.Delay(250);
            Require(undo.Visibility==Visibility.Collapsed,"Remount did not read current undo state");
            vm.CanUndoRefresh=true; await Task.Delay(250);
            Require(undo.Visibility==Visibility.Visible,"Remounted header lost subscription");
            slot.Children.Remove(header);
        });
        await Check("PartitionHeaderUndoSurvivesRemount", async () => {
            var (page,f)=PartitionPage(); await page.InitializeCommand.ExecuteAsync(null);
            var header=new BiliCopilot.UI.Controls.VideoPartition.VideoPartitionMainHeader {ViewModel=page};
            Grid.SetColumn(header,1); slot.Children.Add(header); await Task.Delay(200);
            var undo=(Button)header.FindName("UndoRefreshButton");
            page.CanUndoRefresh=true; await Task.Delay(250);
            Require(undo.Visibility==Visibility.Visible,"Initial partition undo is hidden");
            slot.Children.Remove(header); await Task.Delay(150); page.CanUndoRefresh=false; await Task.Delay(250);
            Require(undo.Visibility==Visibility.Visible,"Detached partition header still responds to old VM");
            slot.Children.Add(header); await Task.Delay(250);
            Require(undo.Visibility==Visibility.Collapsed,"Partition remount retained stale undo");
            page.CanUndoRefresh=true; await Task.Delay(250);
            Require(undo.Visibility==Visibility.Visible,"Partition header lost subscription");
            slot.Children.Remove(header);
        });
        await Check("CoverResizeUnloadFailureAndConsecutiveRecovery", async () => {
            var folder=Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var file=Path.Combine(folder,"controlled-cover.png");
            using(var bitmap=new System.Drawing.Bitmap(1600,900)) {
                using(var drawing=System.Drawing.Graphics.FromImage(bitmap)) { drawing.Clear(System.Drawing.Color.CornflowerBlue); drawing.FillRectangle(System.Drawing.Brushes.Gold,400,225,800,450); }
                bitmap.Save(file,System.Drawing.Imaging.ImageFormat.Png);
            }
            var cover=new BiliCopilot.UI.Controls.RecommendationCoverImage {Width=240,Height=135,Source=new Uri(file)};
            slot.Children.Add(cover);
            var inner=(Image)((Grid)cover.Content).Children[0];
            async Task Until(Func<bool> condition,string message) {for(var i=0;i<80&&!condition();i++)await Task.Delay(100);Require(condition(),message);}
            await Until(()=>inner.Source is not null,"Initial cover never decoded");
            var first=(Microsoft.Graphics.Canvas.UI.Xaml.CanvasImageSource)inner.Source;
            var scale=cover.XamlRoot.RasterizationScale;
            Require(first.SizeInPixels.Width==(uint)Math.Ceiling(240*scale),"Decode ignores physical width");
            Require(first.SizeInPixels.Height==(uint)Math.Ceiling(135*scale),"Decode ignores physical height");
            cover.Width=320;cover.Height=180;
            await Until(()=>inner.Source is not null&&!ReferenceEquals(inner.Source,first),"Resize did not replace decode");
            var second=(Microsoft.Graphics.Canvas.UI.Xaml.CanvasImageSource)inner.Source;
            Require(second.SizeInPixels.Width==(uint)Math.Ceiling(320*scale),"Resize decode width incorrect");
            var device=Microsoft.Graphics.Canvas.CanvasDevice.GetSharedDevice();
            typeof(BiliCopilot.UI.Controls.RecommendationCoverImage).GetMethod("OnDeviceLost",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(cover,new object[]{device,null!});
            await Until(()=>inner.Source is not null&&!ReferenceEquals(inner.Source,second),"First device signal did not reload");
            var recovered=inner.Source;typeof(BiliCopilot.UI.Controls.RecommendationCoverImage).GetMethod("OnDeviceLost",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(cover,new object[]{device,null!});
            await Until(()=>inner.Source is not null&&!ReferenceEquals(inner.Source,recovered),"Consecutive device signal dropped");
            slot.Children.Remove(cover); await Task.Delay(200);
            Require(inner.Source is null,"Unload retained image surface");
            slot.Children.Add(cover);await Until(()=>inner.Source is not null,"Remount failed");
            var bad=Path.Combine(folder,"invalid-cover.png");File.WriteAllText(bad,"not an image");
            cover.Source=new Uri(bad);await Task.Delay(500);
            Require(inner.Source is null,"Failed new source retained stale image");
            cover.Source=new Uri(file);await Until(()=>inner.Source is not null,"Recovery after invalid source failed");
            Trace.Write("cover-result",new{scale,width=second.SizeInPixels.Width,height=second.SizeInPixels.Height,deviceSignal="event callback simulation on healthy device; hardware removal NOT verified"});
            slot.Children.Remove(cover);
        });
        // Leave the actual header/list together for final visual inspection.
        New("Long");await Task.Delay(800);
        var visibleHeader=new BiliCopilot.UI.Controls.Popular.PopularMainHeader();
        Grid.SetColumn(visibleHeader,1);slot.Children.Add(visibleHeader);
        SettingsToolkit.Incremental=true;service.Scenario="Fresh";service.Page=0;service.Batch=2;
        await vm.RefreshVideoCommand.ExecuteAsync(null);await Task.Delay(500);
        var path=Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path,"integration-results.json");
        File.WriteAllText(path,JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        status.Text="Integration checks finished: "+path;
    }
}
public class PartitionFixture:DispatchProxy
{
    public Func<long,Task<(IReadOnlyList<VideoInformation>,long)>> Recommend = _=>Task.FromResult<(IReadOnlyList<VideoInformation>,long)>(([new VideoInformation(default,null,bvId:"recommend")],1));
    protected override object Invoke(MethodInfo method, object[] args) => method.Name switch
    {
        "GetPartitionRecommendVideoListAsync" => Recommend((long)args[1]),
        "GetChildPartitionVideoListAsync" => Task.FromResult<(IReadOnlyList<VideoInformation>,long,int)>(([new VideoInformation(default,null,bvId:"child")],2,1)),
        _ => throw new NotSupportedException(method.Name)
    };
}

