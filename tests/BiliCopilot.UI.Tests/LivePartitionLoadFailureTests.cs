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
        var service = new LiveDiscovery
        {
            List = (_, _) => Task.FromException<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
                new InvalidOperationException("无法获取到有效的直播间列表")),
        };
        var detail = Detail(service);

        await detail.InitializeCommand.ExecuteAsync(null);
        Assert.False(detail.IsLiveLoading);
        Assert.Empty(detail.Rooms);

        service.List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
            ([Room("1")], [Tag("t1")], 2));
        await RefreshAsync(detail);

        Assert.Equal("t1", detail.CurrentTag?.Id);
        Assert.Equal("1", Assert.Single(detail.Rooms).Data.Identifier.Id);
    }

    [Fact]
    public async Task EmptyTagListShowsRoomsWithoutSelectableTagAndRecoversOnRefresh()
    {
        var service = new LiveDiscovery
        {
            List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
                ([Room("1")], [], 0)),
        };
        var detail = Detail(service);

        await detail.InitializeCommand.ExecuteAsync(null);

        Assert.Equal("1", Assert.Single(detail.Rooms).Data.Identifier.Id);
        Assert.Null(detail.CurrentTag);
        // 选择器的输入必须保持为空：空列表会让它把「空列表 + 失效选中项」交给 SlidingTabBar。
        Assert.Null(detail.Children);

        var requests = 0;
        service.List = (_, _) =>
        {
            requests++;
            return Task.FromResult<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
                ([Room("1")], [], 0));
        };
        await detail.LoadRoomsCommand.ExecuteAsync(null);
        Assert.Equal(0, requests);
        Assert.Single(detail.Rooms);

        service.List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
            ([Room("2")], [Tag("t1"), Tag("t2")], 5));
        await RefreshAsync(detail);

        Assert.Equal("t1", detail.CurrentTag?.Id);
        Assert.Equal(2, detail.Children?.Count);
        Assert.Equal("2", Assert.Single(detail.Rooms).Data.Identifier.Id);
    }

    [Fact]
    public async Task FailedLoadExposesRetryableStateAndReinitializesOnNextEntry()
    {
        var service = new LiveDiscovery
        {
            List = (_, _) => Task.FromException<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
                new IOException("offline")),
        };
        var detail = Detail(service);
        var initialized = 0;
        detail.Initialized += (_, _) => initialized++;

        await detail.InitializeCommand.ExecuteAsync(null);

        Assert.True(detail.IsLoadFailed);
        Assert.False(detail.IsLiveLoading);
        Assert.Equal(1, initialized);
        Assert.Null(detail.Children);
        Assert.Null(detail.CurrentTag);
        Assert.Empty(detail.Rooms);

        // 重新进入该分区时页面会再次初始化：失败状态必须允许重试并恢复。
        service.List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
            ([Room("1")], [Tag("t1")], 1));
        await detail.InitializeCommand.ExecuteAsync(null);

        Assert.False(detail.IsLoadFailed);
        Assert.Equal("t1", detail.CurrentTag?.Id);
        Assert.Equal(1, detail.Children?.Count);
        Assert.Equal("1", Assert.Single(detail.Rooms).Data.Identifier.Id);
        Assert.Equal(2, initialized);
    }

    [Fact]
    public async Task LoadedTagsSelectTheFirstTagAndPagingFollowsTheReturnedCursor()
    {
        var service = new LiveDiscovery
        {
            List = (tag, pageNumber) => Task.FromResult<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
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
        var service = new LiveDiscovery
        {
            List = (_, _) => Task.FromException<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
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
        Assert.True(failed.IsLoadFailed);
        Assert.Empty(failed.Rooms);

        service.List = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
            ([Room("1")], [Tag("t1")], 1));

        // 离开该主分区再回来：复用的详情页必须重试，而不是停留在失败残留的空状态。
        await page.SelectMainSectionCommand.ExecuteAsync(page.MainSections[0]);
        PageTestServices.Dispatcher.Drain();
        await page.SelectMainSectionCommand.ExecuteAsync(network);
        PageTestServices.Dispatcher.Drain();

        Assert.Same(failed, page.SelectedDetail);
        await failed.InitializeCommand.ExecutionTask!;
        Assert.False(failed.IsLoadFailed);
        Assert.Equal("t1", failed.CurrentTag?.Id);
        Assert.Equal("1", Assert.Single(failed.Rooms).Data.Identifier.Id);
    }

    private static async Task RefreshAsync(LivePartitionDetailViewModel detail)
    {
        var refresh = detail.RefreshCommand;
        Assert.Null(Record.Exception(() => refresh.Execute(null)));
        if (refresh is IAsyncRelayCommand asyncRefresh)
        {
            await asyncRefresh.ExecutionTask!;
        }

        PageTestServices.Dispatcher.Drain();
    }

    private static LivePartitionDetailViewModel Detail(LiveDiscovery service)
        => new(new PartitionViewModel(new Partition("9", "网游")), service);

    private static LiveTag Tag(string id) => new(id, id, "default");

    private static LiveInformation Room(string id) => new(new MediaIdentifier(id, id, null));

    private sealed class LiveDiscovery : ILiveDiscoveryService
    {
        public Func<LiveTag?, int, Task<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>> List { get; set; }
            = (_, _) => Task.FromResult<(IReadOnlyList<LiveInformation>, IReadOnlyList<LiveTag>?, int)>(
                ([Room("1")], [Tag("t1")], 1));

        public List<(string? Tag, int PageNumber)> Calls { get; } = new();

        public Func<Task<IReadOnlyList<Partition>>>? Partitions { get; set; }

        public Task<(IReadOnlyList<LiveInformation>? Follows, IReadOnlyList<LiveInformation>? Recommend, int NextPageNumber)> GetFeedAsync(int pageNumber = 0, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<LiveInformation>?, IReadOnlyList<LiveInformation>?, int)>((null, null, 0));

        public Task<IReadOnlyList<Partition>> GetLivePartitionsAsync(CancellationToken cancellationToken = default)
            => Partitions?.Invoke() ?? Task.FromResult<IReadOnlyList<Partition>>([]);

        public Task<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)> GetPartitionLiveListAsync(Partition partion, LiveTag? tag = default, int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            Calls.Add((tag?.Id, pageNumber));
            return List(tag, pageNumber);
        }
    }
}
