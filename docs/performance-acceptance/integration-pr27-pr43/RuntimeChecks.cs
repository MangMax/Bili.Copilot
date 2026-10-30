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
            held.SetException(new IOException("late"));await refresh;
            Require(page.RefreshTipState.Status==RefreshTipStatus.Hidden,"Old failure published into child");
        });
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
