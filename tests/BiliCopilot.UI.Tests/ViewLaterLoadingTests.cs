// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

public sealed class ViewLaterLoadingTests
{
    [Fact]
    public async Task FailedInitialLoadShowsErrorAndExplicitRetryRecovers()
    {
        var service = new Service(page => page == 0 ? [Video("123")] : []);
        service.Fail = true;
        var vm = new ViewLaterPageViewModel(service, NullLogger<ViewLaterPageViewModel>.Instance);
        await vm.InitializeCommand.ExecuteAsync(null);
        Assert.True(vm.IsLoadFailed);
        Assert.False(vm.ShowEmpty);
        Assert.False(vm.IsLoading);
        Assert.Empty(vm.Videos);
        await vm.LoadVideosCommand.ExecuteAsync(null);
        Assert.Equal(1, service.Calls);
        service.Fail = false;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(vm.IsLoadFailed);
        Assert.False(vm.ShowEmpty);
        Assert.Equal("123", Assert.Single(vm.Videos).Data.Identifier.Id);
        Assert.Equal(2, service.Calls);
    }

    [Fact]
    public async Task SuccessfulEmptyResponseShowsEmptyAndDoesNotReloadAutomatically()
    {
        var service = new Service(_ => []);
        var vm = new ViewLaterPageViewModel(service, NullLogger<ViewLaterPageViewModel>.Instance);
        await vm.InitializeCommand.ExecuteAsync(null);
        Assert.True(vm.ShowEmpty);
        Assert.False(vm.IsLoadFailed);
        await vm.InitializeCommand.ExecuteAsync(null);
        await vm.LoadVideosCommand.ExecuteAsync(null);
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task FailedNextPagePreservesAlreadyLoadedVideos()
    {
        var service = new Service(_ => [Video("123")]) { Count = 2 };
        var vm = new ViewLaterPageViewModel(service, NullLogger<ViewLaterPageViewModel>.Instance);
        await vm.InitializeCommand.ExecuteAsync(null);
        service.Fail = true;
        await vm.LoadVideosCommand.ExecuteAsync(null);
        Assert.True(vm.IsLoadFailed);
        Assert.False(vm.ShowEmpty);
        Assert.False(vm.IsEmpty);
        Assert.Equal("123", Assert.Single(vm.Videos).Data.Identifier.Id);
        Assert.Equal(2, service.Calls);
    }

    private static VideoInformation Video(string id) => new(new(id, "Video", null), new PublisherProfile(new UserProfile("42")));

    private sealed class Service(Func<int, IReadOnlyList<VideoInformation>> read) : IViewLaterService
    {
        public bool Fail { get; set; }
        public int Count { get; set; } = 1;
        public int Calls { get; private set; }
        public Task<(IReadOnlyList<VideoInformation>? Videos, int Count, int NextPageNumber)> GetViewLaterSetAsync(int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Fail) throw new HttpRequestException("offline");
            var videos = read(pageNumber);
            return Task.FromResult<(IReadOnlyList<VideoInformation>?, int, int)>((videos, videos.Count == 0 ? 0 : Count, pageNumber + 1));
        }
        public Task AddAsync(string aid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveAsync(string[] aids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CleanAsync(ViewLaterCleanType cleanType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
