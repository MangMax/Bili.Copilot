// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Context;
using Richasy.BiliKernel.Bili.Comment;
using Richasy.BiliKernel.Models;

namespace BiliCopilot.UI.Services.AI;

/// <summary>
/// 把 bili-kernel 的评论服务适配为 <see cref="IAICommentSource"/>.
/// </summary>
public sealed class BiliKernelCommentSource : IAICommentSource
{
    private readonly ICommentService _commentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BiliKernelCommentSource"/> class.
    /// </summary>
    /// <param name="commentService">评论服务.</param>
    public BiliKernelCommentSource(ICommentService commentService)
    {
        ArgumentNullException.ThrowIfNull(commentService);

        _commentService = commentService;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIComment>> GetHotCommentsAsync(
        string targetId,
        AIContextTarget target,
        CancellationToken cancellationToken = default)
    {
        var targetType = target == AIContextTarget.Article
            ? CommentTargetType.Article
            : CommentTargetType.Video;
        var view = await _commentService
            .GetCommentsAsync(targetId, targetType, CommentSortType.Hot, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return view?.Comments is null || view.Comments.Count == 0
            ? Array.Empty<AIComment>()
            : view.Comments.Select(item => new AIComment(item.Content.Text)).ToArray();
    }
}