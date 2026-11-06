// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Tests;

public sealed class VideoCardMetadataProviderTests
{
    [Fact]
    public async Task SharedMetadataIncludesClassificationAndSurvivesConsumerCancellation()
    {
        var source = new TaskCompletionSource<VideoInformation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        CancellationToken networkToken = default;
        var provider = new VideoCardMetadataProvider((_, token) => { calls++; networkToken = token; return source.Task; });
        var identifier = new MediaIdentifier("123", "Video", null);
        using var cancellation = new CancellationTokenSource();
        var first = provider.GetAsync(identifier, cancellation.Token);
        var second = provider.GetAsync(identifier);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(networkToken.IsCancellationRequested);
        source.SetResult(Video("123"));
        var metadata = await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("科技", metadata.TagName);
        Assert.Equal("42", metadata.PublisherId);
        Assert.Equal(1700000000, metadata.PublishTime!.Value.ToUnixTimeSeconds());
        Assert.Equal(new Uri("https://example.com/avatar.jpg"), metadata.Avatar);
        Assert.Equal(metadata.Avatar, provider.GetKnownAvatar("42"));
        Assert.Same(metadata, await provider.GetAsync(identifier));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task WrongVideoIsRejectedWithoutPoisoningCacheAndCanRetry()
    {
        var calls = 0;
        var provider = new VideoCardMetadataProvider((_, _) => Task.FromResult(Video(++calls == 1 ? "999" : "123")));
        var identifier = new MediaIdentifier("123", "Video", null);
        await Assert.ThrowsAsync<InvalidDataException>(() => provider.GetAsync(identifier));
        Assert.Null(provider.GetKnownAvatar("42"));
        Assert.Equal("科技", (await provider.GetAsync(identifier)).TagName);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CapacityEvictsOldestVideoWithoutClearingOtherMetadata()
    {
        var calls = 0;
        var provider = new VideoCardMetadataProvider((identifier, _) => { calls++; return Task.FromResult(Video(identifier.Id)); });
        for (var id = 1; id <= 257; id++) await provider.GetAsync(new(id.ToString(), "Video", null));
        await provider.GetAsync(new("257", "Video", null));
        Assert.Equal(257, calls);
        await provider.GetAsync(new("1", "Video", null));
        Assert.Equal(258, calls);
    }

    [Fact]
    public async Task MetadataRequestsRespectFourRequestBudget()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximum = 0;
        var provider = new VideoCardMetadataProvider(async (identifier, _) =>
        {
            var running = Interlocked.Increment(ref active);
            maximum = Math.Max(maximum, running);
            await release.Task;
            Interlocked.Decrement(ref active);
            return Video(identifier.Id);
        });
        var queries = Enumerable.Range(1, 5).Select(id => provider.GetAsync(new(id.ToString(), "Video", null))).ToArray();
        Assert.Equal(4, active);
        release.SetResult();
        Assert.Equal(5, (await Task.WhenAll(queries).WaitAsync(TimeSpan.FromSeconds(5))).Length);
        Assert.Equal(4, maximum);
        Assert.Equal(0, active);
    }

    [Fact]
    public async Task ClassificationDeduplicatesCreatorIdsIncludingTheOwner()
    {
        var owner = new PublisherProfile(new UserProfile("42", "Owner", null));
        var guest = new PublisherProfile(new UserProfile("43", "Guest", null));
        var info = new VideoInformation(new("123", "Video", null), owner, collaborators: [owner, guest, guest]);
        info.AddExtensionIfNotNull(VideoExtensionDataId.IsChargingExclusive, true);
        var provider = new VideoCardMetadataProvider((_, _) => Task.FromResult(info));
        var metadata = await provider.GetAsync(info.Identifier);
        Assert.Equal(2, metadata.CreatorCount);
        Assert.True(metadata.IsCollaborativeSubmission);
        Assert.True(metadata.IsChargingExclusive);
    }

    [Fact]
    public void OwnerIsCountedWhenStaffDoesNotRepeatTheOwner()
    {
        var info = new VideoInformation(new("123", "Video", null), new(new UserProfile("42", "Owner", null)),
            collaborators: [new(new UserProfile("43", "Guest", null)), new(new UserProfile("44", "Guest", null))]);
        var metadata = VideoCardMetadata.FromVideo(info);
        Assert.Equal(3, metadata.CreatorCount);
        Assert.True(metadata.IsCollaborativeSubmission);
    }

    [Fact]
    public void OrdinaryVideoDoesNotInferClassificationFromText()
    {
        var info = Video("123");
        info.AddExtensionIfNotNull(VideoExtensionDataId.RecommendReason, "联合投稿 充电专属");
        var metadata = VideoCardMetadata.FromVideo(info);
        Assert.Equal(1, metadata.CreatorCount);
        Assert.False(metadata.IsCollaborativeSubmission);
        Assert.False(metadata.IsChargingExclusive);
    }

    [Fact]
    public void ExplicitCollaborationSurvivesUnavailableStaffList()
    {
        var info = Video("123");
        info.AddExtensionIfNotNull(VideoExtensionDataId.IsCollaborativeSubmission, true);
        var metadata = VideoCardMetadata.FromVideo(info);
        Assert.True(metadata.IsCollaborativeSubmission);
        Assert.Equal(1, metadata.CreatorCount);
        Assert.False(metadata.IsChargingExclusive);
    }

    private static VideoInformation Video(string id)
    {
        var info = new VideoInformation(new(id, "Video", null), new PublisherProfile(new UserProfile("42", "作者",
            new BiliImage(new Uri("https://example.com/avatar.jpg")))), publishTime: DateTimeOffset.FromUnixTimeSeconds(1700000000));
        info.AddExtensionIfNotNull(VideoExtensionDataId.TagName, "科技");
        return info;
    }
}
