// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手控制器对当前播放进度给出的决策.
/// </summary>
/// <param name="Kind">需要执行的动作.</param>
/// <param name="Segment">相关的片段.</param>
/// <param name="TargetPosition">跳转目标位置（秒），仅在 <see cref="SponsorBlockActionKind.Skip"/> 时有效.</param>
/// <param name="MuteState">
/// 需要应用的静音状态。仅在 <see cref="SponsorBlockActionKind.Mute"/> 与
/// <see cref="SponsorBlockActionKind.RestoreMute"/> 时有效.
/// </param>
public readonly record struct SponsorBlockDecision(
    SponsorBlockActionKind Kind,
    SponsorBlockSegment? Segment = null,
    double TargetPosition = 0,
    bool? MuteState = null)
{
    /// <summary>
    /// 是否需要执行动作.
    /// </summary>
    public bool IsActionable => Kind != SponsorBlockActionKind.None;
}
