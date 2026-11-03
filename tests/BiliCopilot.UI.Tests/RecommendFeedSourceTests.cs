using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.ViewModels.Recommend;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

public sealed class RecommendFeedSourceTests
{
    [Fact]
    public async Task MobileSourceForwardsCursorAndReturnsNextOne()
    {
        var service = new FakeService { Recommend = (offset, _) => ([offset == 0 ? "a" : "b"], offset + 1) };
        var source = new MobileRecommendFeedSource(service);

        var (videos, offset) = await source.FetchAsync(3);

        Assert.Equal("b", videos[0].Identifier.Id);
        Assert.Equal(4, offset);
        Assert.Equal(3, service.LastRecommendOffset);
        Assert.Equal(RecommendFeedSource.Mobile, source.Kind);
    }

    [Fact]
    public async Task WebSourceAsksForTheConfiguredPageSize()
    {
        var service = new FakeService { Curated = (offset, size) => (["web"], offset + 1) };
        var source = new WebRecommendFeedSource(service, pageSize: 20);

        var (videos, offset) = await source.FetchAsync(1);

        Assert.Equal("web", videos[0].Identifier.Id);
        Assert.Equal(2, offset);
        Assert.Equal(20, service.LastCuratedPageSize);
        Assert.Equal(1, service.LastCuratedOffset);
    }

    [Fact]
    public async Task MergedSourceInterleavesDeduplicatesAndReportsFallback()
    {
        var service = new FakeService
        {
            Recommend = (_, _) => (["a", "shared"], 1),
            Curated = (_, _) => (["shared", "w"], 2),
        };
        var source = new MergedRecommendFeedSource(
            new MobileRecommendFeedSource(service),
            new WebRecommendFeedSource(service));

        var (videos, _) = await source.FetchAsync(0);

        Assert.Equal(new[] { "a", "shared", "w" }, videos.Select(p => p.Identifier.Id));
    }

    [Fact]
    public async Task MergedSourceFallsBackWhenWebSideFails()
    {
        var service = new FakeService
        {
            Recommend = (_, _) => (["a"], 1),
            Curated = (_, _) => throw new InvalidOperationException("web down"),
        };
        var source = new MergedRecommendFeedSource(
            new MobileRecommendFeedSource(service),
            new WebRecommendFeedSource(service));

        var (videos, _) = await source.FetchAsync(0);

        // 单路失败时静默使用另一路，不再提示来源。
        Assert.Equal("a", Assert.Single(videos).Identifier.Id);
    }

    [Fact]
    public async Task MergedSourceKeepsItsOwnWebCursor()
    {
        var service = new FakeService
        {
            Recommend = (offset, _) => (["a"], offset + 1),
            Curated = (offset, _) => (["w"], offset + 1),
        };
        var source = new MergedRecommendFeedSource(
            new MobileRecommendFeedSource(service),
            new WebRecommendFeedSource(service));

        await source.FetchAsync(0);
        await source.FetchAsync(5);

        // 网页端自己从 0 推进到 1，不受页面游标 5 影响。
        Assert.Equal(1, service.LastCuratedOffset);
        Assert.Equal(5, service.LastRecommendOffset);
    }

    private static VideoInformation Video(string id) => new(new(id, id, null!), null!, bvId: id);

    private sealed class FakeService : IVideoDiscoveryService
    {
        public Func<long, CancellationToken, (string[] Ids, long Offset)> Recommend { get; set; }
            = (_, _) => (["recommend"], 1);

        public Func<long, int, (string[] Ids, long Offset)> Curated { get; set; }
            = (_, _) => (["curated"], 1);

        public long LastRecommendOffset { get; private set; } = -1;
        public long LastCuratedOffset { get; private set; } = -1;
        public int LastCuratedPageSize { get; private set; }

        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetRecommendVideoListAsync(long offset = 0, CancellationToken cancellationToken = default)
        {
            LastRecommendOffset = offset;
            var (ids, next) = Recommend(offset, cancellationToken);
            return Task.FromResult<(IReadOnlyList<VideoInformation>, long)>((ids.Select(Video).ToList(), next));
        }

        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetCuratedFeedAsync(long freshIndex = 0, int pageSize = 0, CancellationToken cancellationToken = default)
        {
            LastCuratedOffset = freshIndex;
            LastCuratedPageSize = pageSize;
            var (ids, next) = Curated(freshIndex, pageSize);
            return Task.FromResult<(IReadOnlyList<VideoInformation>, long)>((ids.Select(Video).ToList(), next));
        }

        public Task<IReadOnlyList<Partition>> GetVideoPartitionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VideoInformation>> GetCuratedPlaylistAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetHotVideoListAsync(long offset = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VideoInformation>> GetGlobalRankingListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VideoInformation>> GetPartitionRankingListAsync(Partition partition, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> GetPartitionRecommendVideoListAsync(Partition partition, long offset = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset, int NextPageNumber)> GetChildPartitionVideoListAsync(Partition childPartition, long offset = 0, int pageNumber = 0, PartitionVideoSortType sortType = PartitionVideoSortType.Default, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}