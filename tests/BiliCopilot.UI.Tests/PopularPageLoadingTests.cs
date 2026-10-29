// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.View;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.Models.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

public sealed class PopularPageLoadingTests
{
    [Theory]
    [InlineData("Recommend")]
    [InlineData("Hot")]
    public async Task BuiltInContentAppearsWhilePartitionsArePending(string savedSection)
    {
        var service = new DiscoveryService();
        var page = CreatePage(service, savedSection);
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        PageTestServices.Dispatcher.Drain();

        Assert.False(service.Partitions.Task.IsCompleted);
        Assert.Single(page.Videos);
        Assert.Equal(savedSection, page.Videos[0].Data.BvId);
        Assert.Equal(new[] { "partitions", savedSection }, service.Requests);

        service.Partitions.SetResult([]);
        await initialization;
    }

    private static PopularPageViewModel CreatePage(DiscoveryService service, string savedSection)
    {
        SettingsToolkit.LastSection = savedSection;
        PageTestServices.Dispatcher = new();
        return new(service, NullLogger<PopularPageViewModel>.Instance);
    }

    [Fact]
    public async Task SavedPartitionWaitsForDirectoryThenLoadsThatPartition()
    {
        var service = new DiscoveryService();
        var page = CreatePage(service, "13");
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        PageTestServices.Dispatcher.Drain();
        Assert.Null(page.SelectedSection);
        Assert.Empty(page.Videos);

        service.Partitions.SetResult([new Partition("13", "Anime")]);
        await initialization;
        PageTestServices.Dispatcher.Drain();
        Assert.Equal("13", Assert.Single(page.Videos).Data.BvId);
        Assert.Equal("13", SettingsToolkit.LastSection);
        Assert.False(page.IsPartitionLoading);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableSavedPartitionFallsBackToRecommend(bool fails)
    {
        var service = new DiscoveryService();
        var page = CreatePage(service, "13");
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        if (fails) service.Partitions.SetException(new IOException("Directory unavailable"));
        else service.Partitions.SetResult([new Partition("1", "Other")]);
        await initialization;
        PageTestServices.Dispatcher.Drain();

        Assert.Equal("Recommend", Assert.Single(page.Videos).Data.BvId);
        Assert.Equal("Recommend", SettingsToolkit.LastSection);
        Assert.False(page.IsPartitionLoading);
    }

    [Theory]
    [InlineData("Hot")]
    [InlineData("Recommend")]
    public async Task DirectoryFailureDoesNotReplaceBuiltInContent(string savedSection)
    {
        var service = new DiscoveryService();
        var page = CreatePage(service, savedSection);
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        PageTestServices.Dispatcher.Drain();
        var video = Assert.Single(page.Videos);
        service.Partitions.SetException(new IOException("Directory unavailable"));
        await initialization;
        PageTestServices.Dispatcher.Drain();

        Assert.Same(video, Assert.Single(page.Videos));
        Assert.Equal(savedSection, SettingsToolkit.LastSection);
        Assert.False(page.IsPartitionLoading);
    }

    [Fact]
    public async Task UserSelectionWinsOverPendingPartitionRestoration()
    {
        var service = new DiscoveryService();
        var page = CreatePage(service, "13");
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        page.SelectSectionCommand.Execute(page.Sections.OfType<PopularSectionItemViewModel>().Single(x => x.Type == PopularSectionType.Hot));
        PageTestServices.Dispatcher.Drain();
        service.Partitions.SetResult([new Partition("13", "Anime")]);
        await initialization;
        PageTestServices.Dispatcher.Drain();

        Assert.Equal("Hot", Assert.Single(page.Videos).Data.BvId);
        Assert.Equal("Hot", SettingsToolkit.LastSection);
    }

    [Fact]
    public async Task QueuedSwitchesKeepCacheOwnedByDisplayedSection()
    {
        var service = new DiscoveryService();
        var page = CreatePage(service, "Recommend");
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        PageTestServices.Dispatcher.Drain();
        var recommend = Assert.Single(page.Videos);
        var hot = page.Sections.OfType<PopularSectionItemViewModel>().Single(x => x.Type == PopularSectionType.Hot);
        var rank = page.Sections.OfType<PopularSectionItemViewModel>().Single(x => x.Type == PopularSectionType.Rank);
        page.SelectSectionCommand.Execute(hot);
        page.SelectSectionCommand.Execute(rank);
        PageTestServices.Dispatcher.Drain();
        Assert.Equal("Rank", Assert.Single(page.Videos).Data.BvId);
        page.SelectSectionCommand.Execute(hot);
        PageTestServices.Dispatcher.Drain();
        Assert.Equal("Hot", Assert.Single(page.Videos).Data.BvId);
        page.SelectSectionCommand.Execute(page.Sections[0]);
        PageTestServices.Dispatcher.Drain();
        Assert.Same(recommend, Assert.Single(page.Videos));
        service.Partitions.SetResult([]);
        await initialization;
    }

    [Fact]
    public async Task ReinitializationDoesNotDuplicateSectionsOrContent()
    {
        var service = new DiscoveryService();
        var page = CreatePage(service, "Recommend");
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        await page.InitializeCommand.ExecuteAsync(null);
        PageTestServices.Dispatcher.Drain();
        Assert.Equal(3, page.Sections.Count);
        Assert.Single(page.Videos);
        service.Partitions.SetResult([]);
        await initialization;
    }

    [Fact]
    public async Task RefreshBeforePartitionRestorationIsHarmless()
    {
        var service = new DiscoveryService();
        var page = CreatePage(service, "13");
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        await page.RefreshVideoCommand.ExecuteAsync(null);
        Assert.Empty(page.Videos);
        service.Partitions.SetResult([new Partition("13", "Anime")]);
        await initialization;
        PageTestServices.Dispatcher.Drain();
        Assert.Equal("13", Assert.Single(page.Videos).Data.BvId);
    }

    [Theory]
    [InlineData("Recommend")]
    [InlineData("Hot")]
    [InlineData("Rank")]
    [InlineData("13")]
    public async Task OldCompletionCannotPublishOrClearNewLoadingState(string savedSection)
    {
        var old = new TaskCompletionSource<IReadOnlyList<VideoInformation>>();
        var current = new TaskCompletionSource<IReadOnlyList<VideoInformation>>();
        var service = new DiscoveryService
        {
            Recommend = async _ => (await old.Task, 42),
            Hot = async _ => (await old.Task, 42),
            Rank = () => old.Task,
        };
        var page = CreatePage(service, savedSection);
        service.Partitions.SetResult([new Partition("13", "Anime")]);
        await page.InitializeCommand.ExecuteAsync(null);
        PageTestServices.Dispatcher.Drain();
        service.Hot = async _ => (await current.Task, 7);
        service.Recommend = async _ => (await current.Task, 7);
        var nextSection = savedSection == "Hot" ? PopularSectionType.Recommend : PopularSectionType.Hot;
        page.SelectSectionCommand.Execute(page.Sections.OfType<PopularSectionItemViewModel>().Single(x => x.Type == nextSection));
        PageTestServices.Dispatcher.Drain();

        old.SetResult([Video("stale")]);
        PageTestServices.Dispatcher.Drain();
        Assert.True(page.IsVideoLoading);
        Assert.Empty(page.Videos);
        current.SetResult([Video("current")]);
        PageTestServices.Dispatcher.Drain();
        Assert.Equal("current", Assert.Single(page.Videos).Data.BvId);
        Assert.False(page.IsVideoLoading);
    }

    [Fact]
    public async Task RefreshReplacesAnInFlightRequestAndResetsItsCursor()
    {
        var pending = new TaskCompletionSource<(IReadOnlyList<VideoInformation>, long)>();
        var offsets = new List<long>();
        var service = new DiscoveryService
        {
            Recommend = offset =>
            {
                offsets.Add(offset);
                return offsets.Count == 1 ? pending.Task
                    : Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([Video("fresh")], 7));
            },
        };
        var page = CreatePage(service, "Recommend");
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        PageTestServices.Dispatcher.Drain();

        await page.RefreshVideoCommand.ExecuteAsync(null);
        Assert.Equal("fresh", Assert.Single(page.Videos).Data.BvId);
        pending.SetResult(([Video("stale")], 42));
        service.Partitions.SetResult([]);
        await initialization;
        PageTestServices.Dispatcher.Drain();
        Assert.Equal("fresh", Assert.Single(page.Videos).Data.BvId);
        await page.LoadVideosCommand.ExecuteAsync(null);
        Assert.Equal(new long[] { 0, 0, 7 }, offsets);
    }

    [Fact]
    public async Task SwitchingSectionsRejectsLateContentAndLoadsCurrentSection()
    {
        var pending = new TaskCompletionSource<(IReadOnlyList<VideoInformation>, long)>();
        var service = new DiscoveryService { Recommend = _ => pending.Task };
        var page = CreatePage(service, "Recommend");
        var initialization = page.InitializeCommand.ExecuteAsync(null);
        PageTestServices.Dispatcher.Drain();
        Assert.True(page.IsVideoLoading);

        page.SelectSectionCommand.Execute(page.Sections.OfType<PopularSectionItemViewModel>().Single(x => x.Type == PopularSectionType.Hot));
        PageTestServices.Dispatcher.Drain();
        Assert.Single(page.Videos);
        Assert.Equal("Hot", page.Videos[0].Data.BvId);

        pending.SetResult(([Video("stale")], 42));
        service.Partitions.SetResult([]);
        await initialization;
        PageTestServices.Dispatcher.Drain();
        Assert.Equal("Hot", Assert.Single(page.Videos).Data.BvId);
        Assert.False(page.IsVideoLoading);
    }

    private sealed class DiscoveryService : IVideoDiscoveryService
    {
        public List<string> Requests { get; } = new();
        public TaskCompletionSource<IReadOnlyList<Partition>> Partitions { get; } = new();
        public Func<long, Task<(IReadOnlyList<VideoInformation>, long)>> Recommend { get; set; }
            = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([Video("Recommend")], 1));
        public Func<long, Task<(IReadOnlyList<VideoInformation>, long)>> Hot { get; set; }
            = _ => Task.FromResult<(IReadOnlyList<VideoInformation>, long)>(([Video("Hot")], 1));
        public Func<Task<IReadOnlyList<VideoInformation>>>? Rank { get; set; }
        public Task<IReadOnlyList<Partition>> GetVideoPartitionsAsync(CancellationToken cancellationToken = default)
        {
            Requests.Add("partitions");
            return Partitions.Task;
        }
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetRecommendVideoListAsync(long offset = 0, CancellationToken cancellationToken = default)
        {
            Requests.Add("Recommend");
            return Recommend(offset);
        }
        public Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetHotVideoListAsync(long offset = 0, CancellationToken cancellationToken = default)
        {
            Requests.Add("Hot");
            return Hot(offset);
        }
        public Task<IReadOnlyList<VideoInformation>> GetGlobalRankingListAsync(CancellationToken cancellationToken = default) => Rank?.Invoke() ?? Task.FromResult<IReadOnlyList<VideoInformation>>([Video("Rank")]);
        public Task<IReadOnlyList<VideoInformation>> GetPartitionRankingListAsync(Partition partition, CancellationToken cancellationToken = default) => Rank?.Invoke() ?? Task.FromResult<IReadOnlyList<VideoInformation>>([Video(partition.Id)]);
        public Task<IReadOnlyList<VideoInformation>> GetCuratedPlaylistAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> GetPartitionRecommendVideoListAsync(Partition partition, long offset = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Videos, long Offset, int NextPageNumber)> GetChildPartitionVideoListAsync(Partition childPartition, long offset = 0, int pageNumber = 0, PartitionVideoSortType sortType = PartitionVideoSortType.Default, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static VideoInformation Video(string id) => new(default, null!, bvId: id);
}

