// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.ObjectModel;
using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 推荐刷新与滚动锚点：增量刷新成功后回到顶部，撤销回到刷新前的视口。
/// </summary>
/// <remarks>
/// 视口用真实的 <see cref="RecommendationScrollAnchor"/> 源文件驱动。测试替身只模拟
/// ItemsRepeater 的窗口化：视口和缓存之外的卡片不在可视树里，因此“找当前已实现卡片”
/// 在撤销时不可靠，必须依赖刷新前记录的绝对偏移。
/// </remarks>
public sealed class RecommendationRefreshScrollTests : IDisposable
{
    private static DispatcherQueue Dispatcher => (DispatcherQueue)NavigationTestServices.Current.Value![typeof(DispatcherQueue)];
    public RecommendationRefreshScrollTests()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new()
        {
            [typeof(DispatcherQueue)] = new DispatcherQueue(),
            [typeof(ILogger<VideoPartitionDetailViewModel>)] = NullLogger<VideoPartitionDetailViewModel>.Instance,
        };
    }

    public void Dispose()
    {
        NavigationTestServices.Current.Value = null;
        NavigationTestServices.Settings.Value = null;
    }

    [Fact]
    public async Task IncrementalRefreshReturnsToTopWhenUserHadScrolled()
    {
        var service = new DiscoveryService();
        var page = await CreatePopularPageAsync(service, 31);
        var ui = CreateUi(page);
        ui.ScrollTo(600);
        UseIncremental(true);
        service.Recommend = FreshBatch("fresh-1", "fresh-2");

        await page.RefreshVideoCommand.ExecuteAsync(null);
        ui.Drain();

        Assert.Equal(0, ui.Scroll.VerticalOffset);
        Assert.Equal(33, page.Videos.Count);
    }

    [Fact]
    public async Task IncrementalRefreshAtTopKeepsNewItemsVisible()
    {
        var service = new DiscoveryService();
        var page = await CreatePopularPageAsync(service, 31);
        var ui = CreateUi(page);
        UseIncremental(true);
        service.Recommend = FreshBatch("fresh-1", "fresh-2");

        await page.RefreshVideoCommand.ExecuteAsync(null);
        ui.Drain();

        Assert.Equal(0, ui.Scroll.VerticalOffset);
    }

    [Fact]
    public async Task UndoRestoresPreRefreshOffsetWhenAnchorCardWasRecycled()
    {
        var service = new DiscoveryService();
        var page = await CreatePopularPageAsync(service, 31);
        var original = page.Videos.ToArray();
        var ui = CreateUi(page);
        ui.ScrollTo(600);
        UseIncremental(true);
        service.Recommend = FreshBatch("fresh-1", "fresh-2");

        await page.RefreshVideoCommand.ExecuteAsync(null);
        ui.Drain();
        ui.Realize();
        Assert.Equal(0, ui.Scroll.VerticalOffset);
        Assert.False(ui.IsRealized(original[2]), "pre-refresh anchor card must be outside the reset window");

        page.UndoRefreshCommand.Execute(null);
        ui.Drain();

        Assert.Equal(600, ui.Scroll.VerticalOffset);
        Assert.Equal(original, page.Videos);
        for (var i = 0; i < original.Length; i++)
        {
            Assert.Same(original[i], page.Videos[i]);
        }
    }

    [Fact]
    public async Task ConsecutiveRefreshesUndoToTheLatestPreRefreshOffset()
    {
        var service = new DiscoveryService();
        var page = await CreatePopularPageAsync(service, 31);
        var ui = CreateUi(page);
        ui.ScrollTo(600);
        UseIncremental(true);
        service.Recommend = FreshBatch("first-1");

        await page.RefreshVideoCommand.ExecuteAsync(null);
        ui.Drain();
        Assert.Equal(0, ui.Scroll.VerticalOffset);

        var beforeSecond = page.Videos.ToArray();
        ui.ScrollTo(400);
        service.Recommend = FreshBatch("second-1");

        await page.RefreshVideoCommand.ExecuteAsync(null);
        ui.Drain();
        Assert.Equal(0, ui.Scroll.VerticalOffset);

        page.UndoRefreshCommand.Execute(null);
        ui.Drain();

        Assert.Equal(400, ui.Scroll.VerticalOffset);
        Assert.Equal(beforeSecond, page.Videos);
    }

    [Fact]
    public async Task UndoBeforeTheQueuedResetRunsStillRestoresTheViewport()
    {
        var service = new DiscoveryService();
        var page = await CreatePopularPageAsync(service, 31);
        var original = page.Videos.ToArray();
        var ui = CreateUi(page);
        ui.ScrollTo(600);
        UseIncremental(true);
        service.Recommend = FreshBatch("fresh-1");

        await page.RefreshVideoCommand.ExecuteAsync(null);
        Assert.Equal(1, ui.Pending);

        page.UndoRefreshCommand.Execute(null);
        ui.Drain();

        Assert.Equal(600, ui.Scroll.VerticalOffset);
        Assert.Equal(original, page.Videos);
    }

    [Fact]
    public async Task CancelDropsTheQueuedResetAfterNavigation()
    {
        var service = new DiscoveryService();
        var page = await CreatePopularPageAsync(service, 31);
        var ui = CreateUi(page);
        ui.ScrollTo(600);
        UseIncremental(true);
        service.Recommend = FreshBatch("fresh-1");

        await page.RefreshVideoCommand.ExecuteAsync(null);
        Assert.Equal(1, ui.Pending);
        ui.Anchor.Cancel();
        ui.Drain();

        Assert.Equal(600, ui.Scroll.VerticalOffset);
    }

    [Fact]
    public async Task FailedRefreshDoesNotChangeTheViewportOrPublishAChange()
    {
        var service = new DiscoveryService();
        var page = await CreatePopularPageAsync(service, 31);
        var ui = CreateUi(page);
        var changes = new List<RecommendationListChange>();
        page.RecommendationListChanging += changes.Add;
        ui.ScrollTo(600);
        UseIncremental(true);
        service.Recommend = _ => Task.FromException<(IReadOnlyList<VideoInformation>, long)>(new IOException("offline"));

        await page.RefreshVideoCommand.ExecuteAsync(null);
        ui.Drain();

        Assert.Empty(changes);
        Assert.Equal(600, ui.Scroll.VerticalOffset);
    }

    [Fact]
    public async Task IncrementalRefreshReportsResetToTopAndUndoReportsUndoRefresh()
    {
        var service = new DiscoveryService();
        var page = await CreatePopularPageAsync(service, 31);
        var ui = CreateUi(page);
        var changes = new List<RecommendationListChange>();
        page.RecommendationListChanging += changes.Add;
        UseIncremental(true);
        service.Recommend = FreshBatch("fresh-1");

        await page.RefreshVideoCommand.ExecuteAsync(null);
        ui.Drain();
        page.UndoRefreshCommand.Execute(null);
        ui.Drain();

        Assert.Equal([RecommendationListChange.ResetToTop, RecommendationListChange.UndoRefresh], changes);
    }

    [Fact]
    public async Task NonIncrementalRefreshReportsPreserveVisibleCard()
    {
        var service = new DiscoveryService();
        var page = await CreatePopularPageAsync(service, 31);
        var ui = CreateUi(page);
        var changes = new List<RecommendationListChange>();
        page.RecommendationListChanging += changes.Add;
        UseIncremental(false);
        service.Recommend = FreshBatch("fresh-1");

        await page.RefreshVideoCommand.ExecuteAsync(null);
        ui.Drain();

        Assert.Equal([RecommendationListChange.PreserveVisibleCard], changes);
    }

    [Fact]
    public async Task PartitionIncrementalRefreshReturnsToTopAndUndoRestoresTheOffset()
    {
        var service = new PartitionDiscoveryService();
        var detail = await CreatePartitionAsync(service, 31);
        var original = detail.Videos.ToArray();
        var ui = CreateUi(detail);
        ui.ScrollTo(600);
        UseIncremental(true);
        service.Fetch = FreshBatch("fresh-1");

        await detail.RefreshCommand.ExecuteAsync(null);
        ui.Drain();
        ui.Realize();
        Assert.Equal(0, ui.Scroll.VerticalOffset);
        Assert.False(ui.IsRealized(original[2]), "pre-refresh anchor card must be outside the reset window");

        detail.UndoRefreshCommand.Execute(null);
        ui.Drain();

        Assert.Equal(600, ui.Scroll.VerticalOffset);
        Assert.Equal(original, detail.Videos);
    }

    [Fact]
    public async Task PartitionNonIncrementalRefreshReportsPreserveVisibleCard()
    {
        var service = new PartitionDiscoveryService();
        var detail = await CreatePartitionAsync(service, 31);
        var ui = CreateUi(detail);
        var changes = new List<RecommendationListChange>();
        detail.RecommendationListChanging += changes.Add;
        UseIncremental(false);
        service.Fetch = FreshBatch("fresh-1");

        await detail.RefreshCommand.ExecuteAsync(null);
        ui.Drain();

        Assert.Equal([RecommendationListChange.PreserveVisibleCard], changes);
    }

    private static void UseIncremental(bool incremental)
        => SettingsToolkit.WriteLocalSetting(SettingNames.IncrementalRecommendRefresh, incremental);

    private static Func<long, Task<(IReadOnlyList<VideoInformation>, long)>> FreshBatch(params string[] ids)
        => offset => offset == 0
            ? Task.FromResult<(IReadOnlyList<VideoInformation>, long)>((ids.Select(Video).ToArray(), 73))
            : Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([], 73));

    private static async Task<PopularPageViewModel> CreatePopularPageAsync(DiscoveryService service, int count)
    {
        service.Partitions.SetResult([]);
        service.Recommend = offset => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(
            offset == 0 ? (Enumerable.Range(0, count).Select(i => Video($"item-{i}")).ToArray(), 41) : ([], 41));
        var page = new PopularPageViewModel(service, NullLogger<PopularPageViewModel>.Instance);
        await page.InitializeCommand.ExecuteAsync(null);
        Dispatcher.Drain();
        Assert.Equal(count, page.Videos.Count);
        return page;
    }

    private static async Task<VideoPartitionDetailViewModel> CreatePartitionAsync(PartitionDiscoveryService service, int count)
    {
        service.Fetch = offset => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(
            offset == 0 ? (Enumerable.Range(0, count).Select(i => Video($"item-{i}")).ToArray(), 41) : ([], 41));
        var detail = new VideoPartitionDetailViewModel(new PartitionViewModel(Parent()), service);
        await detail.InitializeCommand.ExecuteAsync(null);
        Dispatcher.Drain();
        Assert.Equal(count, detail.Videos.Count);
        return detail;
    }

    private static Partition Parent() => new("13", "Anime", children: [new("24", "Child")]);

    private static VideoInformation Video(string id) => new(new(id, id, null!), null!, bvId: id);

    private static AnchorUi CreateUi(PopularPageViewModel page)
    {
        var ui = new AnchorUi(page.Videos);
        page.RecommendationListChanging += ui.Anchor.Capture;
        page.RecommendationListChanged += ui.Anchor.Restore;
        return ui;
    }

    private static AnchorUi CreateUi(VideoPartitionDetailViewModel detail)
    {
        var ui = new AnchorUi(detail.Videos);
        detail.RecommendationListChanging += ui.Anchor.Capture;
        detail.RecommendationListChanged += ui.Anchor.Restore;
        return ui;
    }

    /// <summary>
    /// One-column virtualized card window. Only the viewport plus half a viewport of
    /// cache is realized; <see cref="ScrollView.ScrollTo(double, double, ScrollingScrollOptions)"/>
    /// does not re-realize, matching a real handler that runs before the next layout pass.
    /// </summary>
    private sealed class AnchorUi
    {
        private const double ItemHeight = 258;
        private readonly ObservableCollection<VideoItemViewModel> _items;
        private readonly Dictionary<object, VideoCardControl> _cards = new(ReferenceEqualityComparer.Instance);

        public AnchorUi(ObservableCollection<VideoItemViewModel> items)
        {
            _items = items;
            Control = new IncrementVerticalScrollControl();
            Scroll = Control.View;
            Scroll.ViewportHeight = 300;
            Root = new FrameworkElement { LayoutPass = Realize };
            Anchor = new RecommendationScrollAnchor(Root, Control);
            Realize();
        }

        public FrameworkElement Root { get; }

        public IncrementVerticalScrollControl Control { get; }

        public ScrollView Scroll { get; }

        public RecommendationScrollAnchor Anchor { get; }

        public int Pending => Scroll.DispatcherQueue.PendingCount;

        public void Drain() => Scroll.DispatcherQueue.Drain();

        public void ScrollTo(double offset)
        {
            Scroll.VerticalOffset = offset;
            Realize();
        }

        public bool IsRealized(object item) => _cards.TryGetValue(item, out var card) && Root.Children.Contains(card);

        public void Realize()
        {
            var cache = Scroll.ViewportHeight / 2;
            var top = Scroll.VerticalOffset - cache;
            var bottom = Scroll.VerticalOffset + Scroll.ViewportHeight + cache;
            Root.Children.Clear();
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (!_cards.TryGetValue(item, out var card))
                {
                    _cards[item] = card = new VideoCardControl { ViewModel = item };
                }

                card.LayoutTop = i * ItemHeight;
                card.ActualHeight = ItemHeight;
                if (card.LayoutTop + ItemHeight > top && card.LayoutTop < bottom)
                {
                    Root.Children.Add(card);
                }
            }

            Scroll.ExtentHeight = _items.Count * ItemHeight;
        }
    }

    private sealed class DiscoveryService : IVideoDiscoveryService
    {
        public TaskCompletionSource<IReadOnlyList<Partition>> Partitions { get; } = new();
        public Func<long, Task<(IReadOnlyList<VideoInformation>, long)>> Recommend { get; set; }
            = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([], 0));
        public Task<IReadOnlyList<Partition>> GetVideoPartitionsAsync(CancellationToken cancellationToken = default) => Partitions.Task;
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetRecommendVideoListAsync(long offset = 0, int pageSize = 0, CancellationToken cancellationToken = default) => Recommend(offset);
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetHotVideoListAsync(long offset = 0, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([Video("Hot")], 1));
        public Task<IReadOnlyList<VideoInformation>> GetGlobalRankingListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VideoInformation>>([Video("Rank")]);
        public Task<IReadOnlyList<VideoInformation>> GetPartitionRankingListAsync(Partition partition, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VideoInformation>>([Video(partition.Id)]);
        public Task<IReadOnlyList<VideoInformation>> GetCuratedPlaylistAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> GetPartitionRecommendVideoListAsync(Partition partition, long offset = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset, int NextPageNumber)> GetChildPartitionVideoListAsync(Partition childPartition, long offset = 0, int pageNumber = 0, PartitionVideoSortType sortType = PartitionVideoSortType.Default, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PartitionDiscoveryService : IVideoDiscoveryService
    {
        public Func<long, Task<(IReadOnlyList<VideoInformation>, long)>> Fetch { get; set; }
            = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([], 0));
        public Task<IReadOnlyList<Partition>> GetVideoPartitionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Partition>>([Parent()]);
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> GetPartitionRecommendVideoListAsync(Partition partition, long offset = 0, CancellationToken cancellationToken = default) => Fetch(offset);
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset, int NextPageNumber)> GetChildPartitionVideoListAsync(Partition partition, long offset = 0, int pageNumber = 0, PartitionVideoSortType sortType = PartitionVideoSortType.Default, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetRecommendVideoListAsync(long offset = 0, int pageSize = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetHotVideoListAsync(long offset = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VideoInformation>> GetPartitionRankingListAsync(Partition partition, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VideoInformation>> GetGlobalRankingListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VideoInformation>> GetCuratedPlaylistAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
