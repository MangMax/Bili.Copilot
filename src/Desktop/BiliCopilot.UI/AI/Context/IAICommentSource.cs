// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 评论来源端口：提供热评.
/// </summary>
public interface IAICommentSource
{
    /// <summary>
    /// 获取目标下的热评.
    /// </summary>
    /// <param name="targetId">视频 Id 或文章 Id.</param>
    /// <param name="target">目标类型.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>热评列表，可能为空.</returns>
    Task<IReadOnlyList<AIComment>> GetHotCommentsAsync(string targetId, AIContextTarget target, CancellationToken cancellationToken = default);
}
