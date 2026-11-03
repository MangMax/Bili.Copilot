// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 采集 AI 请求前的源上下文.
/// </summary>
/// <remarks>
/// 只依赖端口，不引用 WinUI、AgentKernel 或 kernel 模型；进度通过 <see cref="AIContextStage"/> 上报，
/// 本地化文案由上层映射.
/// </remarks>
internal sealed class AIContextCollector
{
    private readonly IAISubtitleSource _subtitleSource;
    private readonly IAICommentSource _commentSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIContextCollector"/> class.
    /// </summary>
    /// <param name="subtitleSource">字幕来源.</param>
    /// <param name="commentSource">评论来源.</param>
    internal AIContextCollector(IAISubtitleSource subtitleSource, IAICommentSource commentSource)
    {
        ArgumentNullException.ThrowIfNull(subtitleSource);
        ArgumentNullException.ThrowIfNull(commentSource);

        _subtitleSource = subtitleSource;
        _commentSource = commentSource;
    }

    /// <summary>
    /// 采集上下文.
    /// </summary>
    /// <param name="request">采集请求.</param>
    /// <param name="progress">进度回调，可为空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>源上下文.</returns>
    /// <exception cref="OperationCanceledException">采集被取消.</exception>
    public async Task<AISourceContext> CollectAsync(
        AIContextRequest request,
        IProgress<AIContextStage>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var subtitleText = string.Empty;
        var subtitleStatus = AISubtitleStatus.NotRequested;
        var commentText = string.Empty;
        var articleText = string.Empty;

        if (request.Target == AIContextTarget.Article)
        {
            progress?.Report(AIContextStage.ArticleContent);
            articleText = AIContextFormatting.ToPlainText(request.HtmlContent);
            cancellationToken.ThrowIfCancellationRequested();
        }
        else
        {
            progress?.Report(AIContextStage.SubtitleMetadata);
            var tracks = await _subtitleSource.GetTracksAsync(request.Id, request.PartId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (tracks is null || tracks.Count == 0)
            {
                subtitleStatus = AISubtitleStatus.Unavailable;
            }
            else
            {
                var preferredIndex = SubtitlePreference.SelectPreferredIndex(tracks, p => p.LanguageName, p => p.IsAI);
                progress?.Report(AIContextStage.SubtitleDetail);
                var cues = await _subtitleSource.GetCuesAsync(tracks[preferredIndex], cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (cues is null || cues.Count == 0)
                {
                    subtitleStatus = AISubtitleStatus.Unavailable;
                }
                else
                {
                    subtitleText = AIContextFormatting.FormatSubtitles(cues);
                    subtitleStatus = AISubtitleStatus.Available;
                }
            }
        }

        if (request.IncludeComments)
        {
            progress?.Report(AIContextStage.Comments);
            var comments = await _commentSource.GetHotCommentsAsync(request.Id, request.Target, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            commentText = AIContextFormatting.FormatComments(comments);
        }

        return new AISourceContext
        {
            Title = request.Title,
            Description = request.Description,
            SubtitleText = subtitleText,
            SubtitleStatus = subtitleStatus,
            CommentText = commentText,
            ArticleText = articleText,
        };
    }
}
