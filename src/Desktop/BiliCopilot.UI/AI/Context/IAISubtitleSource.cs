// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 字幕来源端口：提供字幕元数据与明细.
/// </summary>
public interface IAISubtitleSource
{
    /// <summary>
    /// 获取视频的字幕元数据.
    /// </summary>
    /// <param name="videoId">视频 Id.</param>
    /// <param name="partId">分 P 的 Id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>字幕轨道列表，可能为空.</returns>
    Task<IReadOnlyList<AISubtitleTrack>> GetTracksAsync(string videoId, string? partId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定字幕轨道的明细.
    /// </summary>
    /// <param name="track">字幕轨道.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>字幕条目列表，可能为空.</returns>
    Task<IReadOnlyList<AISubtitleCue>> GetCuesAsync(AISubtitleTrack track, CancellationToken cancellationToken = default);
}
