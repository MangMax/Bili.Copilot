// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Context;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Subtitle;

namespace BiliCopilot.UI.Services.AI;

/// <summary>
/// 把 bili-kernel 的字幕服务适配为 <see cref="IAISubtitleSource"/>.
/// </summary>
public sealed class BiliKernelSubtitleSource : IAISubtitleSource
{
    private readonly ISubtitleService _subtitleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BiliKernelSubtitleSource"/> class.
    /// </summary>
    /// <param name="subtitleService">字幕服务.</param>
    public BiliKernelSubtitleSource(ISubtitleService subtitleService)
    {
        ArgumentNullException.ThrowIfNull(subtitleService);

        _subtitleService = subtitleService;
    }

    /// <inheritdoc/>
    async Task<IReadOnlyList<AISubtitleTrack>> IAISubtitleSource.GetTracksAsync(string videoId, string? partId, CancellationToken cancellationToken)
    {
        var metas = await _subtitleService
            .GetSubtitleMetasAsync(videoId, partId ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);
        return metas is null || metas.Count == 0
            ? Array.Empty<AISubtitleTrack>()
            : metas.Select(meta => new AISubtitleTrack(meta.Id, meta.LanguageName, meta.Url, meta.IsAI)).ToArray();
    }

    /// <inheritdoc/>
    async Task<IReadOnlyList<AISubtitleCue>> IAISubtitleSource.GetCuesAsync(AISubtitleTrack track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);

        var meta = new SubtitleMeta(track.Id, track.LanguageName, track.Url)
        {
            IsAI = track.IsAI,
        };
        var subtitles = await _subtitleService
            .GetSubtitleDetailAsync(meta, cancellationToken)
            .ConfigureAwait(false);
        return subtitles is null || subtitles.Count == 0
            ? Array.Empty<AISubtitleCue>()
            : subtitles.Select(item => new AISubtitleCue(item.StartPosition, item.EndPosition, item.Content)).ToArray();
    }
}