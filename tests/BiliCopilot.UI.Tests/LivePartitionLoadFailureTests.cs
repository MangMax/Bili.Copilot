using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class LivePartitionLoadFailureTests : IDisposable
{
    public LivePartitionLoadFailureTests()
        => NavigationTestServices.Current.Value = new()
        {
            [typeof(Microsoft.Extensions.Logging.ILogger<LivePartitionDetailViewModel>)] = NullLogger<LivePartitionDetailViewModel>.Instance,
            [typeof(ISettingsToolkit)] = new Settings(),
        };

    private sealed class Settings : ISettingsToolkit
    {
        public T ReadLocalSetting<T>(string key, T fallback) => fallback;

        public void WriteLocalSetting<T>(string key, T value) { }
    }

    public void Dispose() => NavigationTestServices.Current.Value = null;

    [Fact]
    public async Task FailedPartitionListRecoversThroughRefreshInsteadOfThrowing()
    {
        var service = new LiveDiscoveryFake
        {
            List = (_, _) => Task.FromException<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                new InvalidOperationException("无法获取到有效的直播间列表")),
        };
        var detail = Detail(service);

        await detail.InitializeCommand.ExecuteAsync(null);
        Assert.False(detail.IsLiveLoading);
        Assert.Empty(detail.Rooms);

        service.List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
            ([Room("1")], [Tag("t1")], 2));
        await RefreshAsync(detail);

        Assert.Equal("t1", detail.CurrentTag?.Id);
        Assert.Equal("1", Assert.Single(detail.Rooms).Data.Identifier.Id);
    }

    [Fact]
    public async Task EmptyTagListShowsRoomsWithoutSelectableTagAndRecoversOnRefresh()
    {
        var service = new LiveDiscoveryFake
        {
            List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                ([Room("1")], [], 0)),
        };
        var detail = Detail(service);

        await detail.InitializeCommand.ExecuteAsync(null);

        Assert.Equal("1", Assert.Single(detail.Rooms).Data.Identifier.Id);
        Assert.Null(detail.CurrentTag);
        // 选择器的输入必须保持为空：空列表会让它把「空列表 + 失效选中项」交给 SlidingTabBar。
        Assert.Null(detail.Children);
        Assert.Null(detail.AvailableTags);

        var requests = 0;
        service.List = (_, _) =>
        {
            requests++;
            return Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                ([Room("1")], [], 0));
        };
        await detail.LoadRoomsCommand.ExecuteAsync(null);
        Assert.Equal(0, requests);
        Assert.Single(detail.Rooms);

        service.List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
            ([Room("2")], [Tag("t1"), Tag("t2")], 5));
        await RefreshAsync(detail);

        Assert.Equal("t1", detail.CurrentTag?.Id);
        Assert.Equal(2, detail.Children?.Count);
        Assert.Equal("2", Assert.Single(detail.Rooms).Data.Identifier.Id);
    }

    [Fact]
    public async Task FailedRoomsLoadLeavesConsistentEmptyStateAndRecoversOnRefresh()
    {
        var failNext = false;
        var service = new LiveDiscoveryFake();
        service.List = (tag, pageNumber) =>
        {
            if (failNext)
            {
                return Task.FromException<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                    new IOException("offline"));
            }

            return Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                tag is null
                    ? ([Room("1")], [Tag("t1")], 4)
                    : ([Room("t1-room")], null, pageNumber + 1));
        };
        var detail = Detail(service);
        var initialized = 0;
        detail.Initialized += (_, _) => initialized++;

        await detail.InitializeCommand.ExecuteAsync(null);
        await detail.LoadRoomsCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "1", "t1-room" }, detail.Rooms.Select(r => r.Data.Identifier.Id));

        // 加载下一页失败：失败收尾必须清空可见列表，且保留可重试的子分区状态。
        failNext = true;
        await detail.LoadRoomsCommand.ExecuteAsync(null);

        Assert.Empty(detail.Rooms);
        Assert.False(detail.IsLiveLoading);
        Assert.Equal("t1", detail.CurrentTag?.Id);
        Assert.Equal(new[] { "t1" }, detail.Children!.Select(t => t.Id));
        Assert.Equal(1, initialized);

        // 重试后恢复。
        failNext = false;
        await RefreshAsync(detail);

        Assert.Equal("t1-room", Assert.Single(detail.Rooms).Data.Identifier.Id);
    }

    [Fact]
    public async Task SwitchingChildPartitionDuringRoomsLoadKeepsLastSelectionAndRequestsIt()
    {
        var t1Response = new TaskCompletionSource<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>();
        var service = new LiveDiscoveryFake();
        service.List = (tag, pageNumber) => tag?.Id switch
        {
            "t1" when pageNumber > 0 => t1Response.Task,
            "t2" => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                ([Room("t2-room")], null, 0)),
            _ => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                ([Room("1")], [Tag("t1"), Tag("t2")], 1)),
        };
        var detail = Detail(service);

        await detail.InitializeCommand.ExecuteAsync(null);
        Assert.Equal("t1", detail.CurrentTag?.Id);

        // t1 的分页请求在飞行中，用户切到 t2。
        var inFlight = detail.LoadRoomsCommand.ExecuteAsync(null);
        Assert.Equal(("t1", 1), service.Calls[^1]);
        detail.ChangeChildPartitionCommand.Execute(Tag("t2"));
        PageTestServices.Dispatcher.Drain();

        // 旧请求随后返回：不得写进已切换的视图，且必须为最后选择补发一次请求。
        t1Response.SetResult(([Room("t1-late")], null, 9));
        await inFlight;
        PageTestServices.Dispatcher.Drain();

        Assert.Equal("t2", detail.CurrentTag?.Id);
        Assert.Equal("t2-room", Assert.Single(detail.Rooms).Data.Identifier.Id);
        Assert.Contains(("t2", 0), service.Calls);
    }

    [Fact]
    public async Task LoadedTagsSelectTheFirstTagAndPagingFollowsTheReturnedCursor()
    {
        var service = new LiveDiscoveryFake
        {
            List = (tag, pageNumber) => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                tag is null
                    ? ([Room("1")], [Tag("t1"), Tag("t2")], 4)
                    : (tag.Id == "t1" ? [Room("t1-room")] : [Room("t2-room")], null, pageNumber + 1)),
        };
        var detail = Detail(service);

        await detail.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "t1", "t2" }, detail.Children!.Select(t => t.Id));
        Assert.Equal("t1", detail.CurrentTag?.Id);
        Assert.Equal("1", Assert.Single(detail.Rooms).Data.Identifier.Id);
        Assert.Equal([(null, 0)], service.Calls);

        await detail.LoadRoomsCommand.ExecuteAsync(null);

        // 分页必须从首次响应带回的游标继续，且当前子分区就是首个标签。
        Assert.Equal([(null, 0), ("t1", 4)], service.Calls);
        Assert.Equal(new[] { "1", "t1-room" }, detail.Rooms.Select(r => r.Data.Identifier.Id));

        detail.ChangeChildPartitionCommand.Execute(Tag("t2"));
        PageTestServices.Dispatcher.Drain();

        Assert.Equal("t2", detail.CurrentTag?.Id);
        Assert.Equal("t2-room", Assert.Single(detail.Rooms).Data.Identifier.Id);
        Assert.Equal(("t2", 0), service.Calls[^1]);
    }

    [Fact]
    public async Task ReenteringAFailedSubPartitionRetriesInsteadOfKeepingTheEmptyDetail()
    {
        var service = new LiveDiscoveryFake
        {
            List = (_, _) => Task.FromException<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
                new InvalidOperationException("无法获取到有效的直播间列表")),
            Partitions = () => Task.FromResult<IReadOnlyList<Partition>>(
                [new Partition("9", "网游", children: [new Partition("90", "全部网游")])]),
        };
        var page = new LivePartitionPageViewModel(service, NullLogger<LivePartitionPageViewModel>.Instance);
        page.InitializeCommand.Execute(null);
        PageTestServices.Dispatcher.Drain();

        var network = page.MainSections.Single(s => s.Data.Id == "9");
        await page.SelectMainSectionCommand.ExecuteAsync(network);
        PageTestServices.Dispatcher.Drain();
        var failed = page.SelectedDetail;
        await failed.InitializeCommand.ExecutionTask!;
        Assert.Empty(failed.Rooms);

        service.List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)>(
            ([Room("1")], [Tag("t1")], 1));

        // 离开该主分区再回来：复用的详情页必须重试，而不是停留在失败残留的空状态。
        await page.SelectMainSectionCommand.ExecuteAsync(page.MainSections[0]);
        PageTestServices.Dispatcher.Drain();
        await page.SelectMainSectionCommand.ExecuteAsync(network);
        PageTestServices.Dispatcher.Drain();

        Assert.Same(failed, page.SelectedDetail);
        await failed.InitializeCommand.ExecutionTask!;
        Assert.Equal("t1", failed.CurrentTag?.Id);
        Assert.Equal("1", Assert.Single(failed.Rooms).Data.Identifier.Id);
    }

    private static async Task RefreshAsync(LivePartitionDetailViewModel detail)
    {
        // RefreshCommand 必须是异步命令：否则测试会静默地不等待刷新完成。
        var refresh = Assert.IsAssignableFrom<IAsyncRelayCommand>(detail.RefreshCommand);
        Assert.Null(Record.Exception(() => refresh.Execute(null)));
        await refresh.ExecutionTask!;
        PageTestServices.Dispatcher.Drain();
    }

    private static LivePartitionDetailViewModel Detail(LiveDiscoveryFake service)
        => new(new PartitionViewModel(new Partition("9", "网游")), service);

    private static LiveTag Tag(string id) => LiveDiscoveryFake.Tag(id);

    private static LiveInformation Room(string id) => LiveDiscoveryFake.Room(id);
}
