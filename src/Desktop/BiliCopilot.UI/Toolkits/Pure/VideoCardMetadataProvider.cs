// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Toolkits;

/// <summary>Public video fields used to complete a card, independent of the signed-in account.</summary>
public sealed record VideoCardMetadata(string? PublisherId, Uri? Avatar, DateTimeOffset? PublishTime, string? TagName,
    int CreatorCount = 0, bool IsCollaborativeSubmission = false, bool IsChargingExclusive = false)
{
    /// <summary>Uses explicit server flags and distinct creator IDs; titles and descriptions are not classification signals.</summary>
    public static VideoCardMetadata FromVideo(VideoInformation video)
    {
        var creators = new HashSet<string>(StringComparer.Ordinal);
        if (video.Publisher?.User?.Id is { Length: > 0 } ownerId && ownerId != "0") creators.Add(ownerId);
        if (video.Collaborators is not null)
        {
            foreach (var collaborator in video.Collaborators)
                if (collaborator?.User?.Id is { Length: > 0 } id && id != "0") creators.Add(id);
        }
        return new(video.Publisher?.User?.Id, video.Publisher?.User?.Avatar?.Uri, video.PublishTime,
            video.GetExtensionIfNotNull<string>(VideoExtensionDataId.TagName), creators.Count,
            creators.Count > 1 || video.GetExtensionIfNotNull<bool?>(VideoExtensionDataId.IsCollaborativeSubmission) is true,
            video.GetExtensionIfNotNull<bool?>(VideoExtensionDataId.IsChargingExclusive) is true);
    }
}

/// <summary>Owns bounded public metadata caching, shared requests and network concurrency for video cards.</summary>
public sealed class VideoCardMetadataProvider(Func<MediaIdentifier, CancellationToken, Task<VideoInformation>> fetch)
{
    private const int Capacity = 256;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _budget = new(4);
    private readonly Dictionary<string, VideoCardMetadata> _cache = new(StringComparer.Ordinal);
    private readonly Queue<string> _cacheOrder = new();
    private readonly Dictionary<string, TaskCompletionSource<VideoCardMetadata>> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Uri> _avatars = new(StringComparer.Ordinal);
    private readonly Queue<string> _avatarOrder = new();

    /// <summary>Returns an already known public avatar without starting a request.</summary>
    public Uri? GetKnownAvatar(string userId)
    {
        lock (_gate) return _avatars.GetValueOrDefault(userId);
    }

    /// <summary>Shares one lookup per video; cancellation only detaches this consumer.</summary>
    public Task<VideoCardMetadata> GetAsync(MediaIdentifier identifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_cache.TryGetValue(identifier.Id, out var cached)) return Task.FromResult(cached);
            if (!_pending.TryGetValue(identifier.Id, out var pending))
            {
                pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending.Add(identifier.Id, pending);
                _ = FetchAsync(identifier, pending);
            }
            return pending.Task.WaitAsync(cancellationToken);
        }
    }

    private async Task FetchAsync(MediaIdentifier identifier, TaskCompletionSource<VideoCardMetadata> completion)
    {
        await _budget.WaitAsync().ConfigureAwait(false);
        VideoCardMetadata? metadata = null;
        Exception? failure = null;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var video = await fetch(identifier, deadline.Token).ConfigureAwait(false);
            if (video.Identifier.Id != identifier.Id) throw new InvalidDataException("Video metadata identity does not match the requested video.");
            metadata = VideoCardMetadata.FromVideo(video);
            lock (_gate)
            {
                if (_cache.Count >= Capacity) _cache.Remove(_cacheOrder.Dequeue());
                _cache.Add(identifier.Id, metadata);
                _cacheOrder.Enqueue(identifier.Id);
                if (metadata.PublisherId is { } userId && metadata.Avatar is { } avatar)
                {
                    if (!_avatars.ContainsKey(userId))
                    {
                        if (_avatars.Count >= Capacity) _avatars.Remove(_avatarOrder.Dequeue());
                        _avatarOrder.Enqueue(userId);
                    }
                    _avatars[userId] = avatar;
                }
            }
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            lock (_gate) _pending.Remove(identifier.Id);
            _budget.Release();
        }
        if (failure is not null) completion.TrySetException(failure);
        else completion.TrySetResult(metadata!);
    }
}
