using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

public sealed class PartitionLoadingRegressionTests : IDisposable
{
    public PartitionLoadingRegressionTests()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new()
        {
            [typeof(ILogger<VideoPartitionDetailViewModel>)] = NullLogger<VideoPartitionDetailViewModel>.Instance,
        };
    }

    public void Dispose()
    {
        NavigationTestServices.Settings.Value = null;
        NavigationTestServices.Current.Value = null;
    }

    [Fact]
    public async Task SidebarSelectionEchoPreservesFirstPendingResponseAndRepeatedInitialization()
    {
        var response = new TaskCompletionSource<(IReadOnlyList<VideoInformation>, long)>();
        var service = new Discovery { Fetch = _ => response.Task };
        var page = new VideoPartitionPageViewModel(service, NullLogger<VideoPartitionPageViewModel>.Instance);
        await page.InitializeCommand.ExecuteAsync(null);
        var detail = page.SelectedPartition!;
        var initial = detail.InitializeCommand.ExecutionTask!;
        // Real ItemsView selection synchronization echoes the restored parent.
        page.SelectPartitionCommand.Execute(page.Partitions[0]);
        await page.InitializeCommand.ExecuteAsync(null);
        response.SetResult(([Video("first")], 1));
        await initial;

        Assert.Same(detail, page.SelectedPartition);
        Assert.Equal("first", Assert.Single(detail.Videos).Data.Identifier.Id);
        Assert.Equal(new long[] { 0 }, service.Offsets);
        Assert.Single(page.Partitions);
        Assert.False(detail.IsVideoLoading);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistedRefreshModeWinsOverPendingLoadMoreAndNextSettingChange(bool incremental)
    {
        var pending = new TaskCompletionSource<(IReadOnlyList<VideoInformation>, long)>();
        var service = new Discovery();
        var detail = new VideoPartitionDetailViewModel(new PartitionViewModel(Parent()), service);
        await detail.InitializeCommand.ExecuteAsync(null);
        var original = Assert.Single(detail.Videos);
        service.Fetch = offset => offset == 1 ? pending.Task : Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([Video("fresh")], 7));
        var load = detail.LoadVideosCommand.ExecuteAsync(null);
        Assert.True(detail.IsVideoLoading);
        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, incremental);
        await detail.RefreshCommand.ExecuteAsync(null);
        pending.SetResult(([Video("stale-page")], 99));
        await load;

        Assert.Equal(new long[] { 0, 1, 0 }, service.Offsets);
        Assert.Equal(incremental ? new[] { "fresh", "original" } : new[] { "fresh" }, detail.Videos.Select(v => v.Data.Identifier.Id));
        Assert.Equal(incremental, detail.CanUndoRefresh);
        detail.UndoRefreshCommand.Execute(null);
        Assert.Equal(incremental ? "original" : "fresh", Assert.Single(detail.Videos).Data.Identifier.Id);

        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, !incremental);
        await detail.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "fresh" }, detail.Videos.Select(v => v.Data.Identifier.Id));
    }

    [Fact]
    public async Task NonIncrementalRefreshReplacesListWithoutUndoAndNotifiesShrink()
    {
        var service = new Discovery();
        var detail = new VideoPartitionDetailViewModel(new PartitionViewModel(Parent()), service);
        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, true);
        service.Fetch = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(
            ([Video("a"), Video("b"), Video("c")], 3));
        await detail.InitializeCommand.ExecuteAsync(null);
        Assert.Equal(3, detail.Videos.Count);

        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, false);
        var updates = 0;
        detail.VideoListUpdated += (_, _) => updates++;
        service.Fetch = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([Video("fresh")], 7));
        await detail.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("fresh", Assert.Single(detail.Videos).Data.Identifier.Id);
        Assert.False(detail.CanUndoRefresh);
        Assert.Equal(1, updates);
        detail.UndoRefreshCommand.Execute(null);
        Assert.Equal("fresh", Assert.Single(detail.Videos).Data.Identifier.Id);

        // 列表通知会驱动主体检查视口；真实加载命令必须从刷新后的游标继续。
        service.Fetch = offset => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(
            ([Video("more1"), Video("more2")], 8));
        await detail.LoadVideosCommand.ExecuteAsync(null);
        Assert.Equal(7, service.Offsets[^1]);
        Assert.Equal(new[] { "fresh", "more1", "more2" }, detail.Videos.Select(v => v.Data.Identifier.Id));
        Assert.Equal(2, updates);
    }

    [Fact]
    public async Task DisablingIncrementalRefreshDropsCachedUndoSnapshots()
    {
        var service = new Discovery();
        var page = new VideoPartitionPageViewModel(service, NullLogger<VideoPartitionPageViewModel>.Instance);
        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, true);
        await page.InitializeCommand.ExecuteAsync(null);
        var detail = page.SelectedPartition!;
        var original = Assert.Single(detail.Videos);
        service.Fetch = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([Video("fresh")], 7));
        await detail.RefreshCommand.ExecuteAsync(null);
        Assert.True(detail.CanUndoRefresh);
        var refreshed = detail.Videos.ToArray();

        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, false);
        page.OnIncrementalRecommendRefreshChanged(false);

        Assert.False(detail.CanUndoRefresh);
        detail.UndoRefreshCommand.Execute(null);
        Assert.Equal(refreshed, detail.Videos);
        Assert.NotSame(original, detail.Videos[0]);
    }

    [Fact]
    public async Task OldPaginationCannotClearPendingRefreshOrOverwriteItsCursor()
    {
        var service = new Discovery();
        var detail = new VideoPartitionDetailViewModel(new PartitionViewModel(Parent()), service);
        await detail.InitializeCommand.ExecuteAsync(null);
        var old = new TaskCompletionSource<(IReadOnlyList<VideoInformation>, long)>();
        var fresh = new TaskCompletionSource<(IReadOnlyList<VideoInformation>, long)>();
        service.Fetch = offset => offset == 1 ? old.Task : fresh.Task;
        var load = detail.LoadVideosCommand.ExecuteAsync(null);
        var refresh = detail.RefreshCommand.ExecuteAsync(null);
        old.SetResult(([Video("stale")], 99));
        await load;
        Assert.True(detail.IsRecommendRefreshing);
        Assert.Equal("original", Assert.Single(detail.Videos).Data.Identifier.Id);
        fresh.SetResult(([Video("fresh")], 7));
        await refresh;
        Assert.Equal("fresh", Assert.Single(detail.Videos).Data.Identifier.Id);
        service.Fetch = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([], 7));
        await detail.LoadVideosCommand.ExecuteAsync(null);
        Assert.Equal(7, service.Offsets[^1]);
        Assert.False(detail.IsVideoLoading);
        Assert.False(detail.IsRecommendRefreshing);
    }

    [Fact]
    public async Task SamePartitionEchoPreservesUndoSnapshot()
    {
        var service = new Discovery();
        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, true);
        var page = new VideoPartitionPageViewModel(service, NullLogger<VideoPartitionPageViewModel>.Instance);
        await page.InitializeCommand.ExecuteAsync(null);
        var detail = page.SelectedPartition!;
        var original = Assert.Single(detail.Videos);
        service.Fetch = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([Video("fresh")], 7));
        await detail.RefreshCommand.ExecuteAsync(null);
        page.SelectPartitionCommand.Execute(page.Partitions[0]);
        Assert.True(detail.CanUndoRefresh);
        detail.UndoRefreshCommand.Execute(null);
        Assert.Same(original, Assert.Single(detail.Videos));
        Assert.Equal(new long[] { 0, 0 }, service.Offsets);
    }


    [Fact]
    public async Task DisableThenReenableMustNotResurrectPartitionInFlightSnapshot()
    {
        var service = new Discovery();
        var detail = new VideoPartitionDetailViewModel(new PartitionViewModel(Parent()), service);
        await detail.InitializeCommand.ExecuteAsync(null);
        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, true);
        var pending = new TaskCompletionSource<(IReadOnlyList<VideoInformation>, long)>();
        service.Fetch = _ => pending.Task;
        var refresh = detail.RefreshCommand.ExecuteAsync(null);
        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, false);
        detail.OnIncrementalRecommendRefreshChanged(false);
        SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, true);
        detail.OnIncrementalRecommendRefreshChanged(true);
        pending.SetResult(([Video("fresh")], 7));
        await refresh;
        Assert.False(detail.CanUndoRefresh);
    }

    private static Partition Parent() => new("13", "Anime", children: [new("24", "Child")]);
    private static VideoInformation Video(string id) => new(new(id, id, null!), null!, bvId: id);

    private sealed class Discovery : IVideoDiscoveryService
    {
        public List<long> Offsets { get; } = new();
        public Func<long, Task<(IReadOnlyList<VideoInformation>, long)>> Fetch { get; set; }
            = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([Video("original")], 1));
        public Task<IReadOnlyList<Partition>> GetVideoPartitionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Partition>>([Parent()]);
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> GetPartitionRecommendVideoListAsync(Partition partition, long offset = 0, CancellationToken cancellationToken = default)
        {
            Offsets.Add(offset);
            return Fetch(offset);
        }
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset, int NextPageNumber)> GetChildPartitionVideoListAsync(Partition partition, long offset = 0, int pageNumber = 0, PartitionVideoSortType sortType = PartitionVideoSortType.Default, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetRecommendVideoListAsync(long offset = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetHotVideoListAsync(long offset = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VideoInformation>> GetPartitionRankingListAsync(Partition partition, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VideoInformation>> GetGlobalRankingListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VideoInformation>> GetCuratedPlaylistAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetCuratedFeedAsync(long freshIndex = 0, int pageSize = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}