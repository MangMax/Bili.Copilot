// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.MpvKernel.Core.Enums;

namespace BiliCopilot.UI.Player;

/// <summary>
/// 长暂停断流后的恢复决策：Idle/未初始化且存在待恢复位置时走 loadfile 重开，
/// 其余情况沿用原生 resume/replay。纯逻辑供视图模型与单元测试共用。
/// </summary>
internal static class LongPauseResumeDecision
{
    /// <summary>
    /// 本次恢复是否应走 loadfile 重开而非 resume：只有长暂停断流留下的待恢复位置才触发。
    /// 断流后 mpv 处于 Idle/未初始化，因此只在非 Playing 的恢复意图下生效；
    /// 直播不经过断流标记，天然豁免。
    /// </summary>
    /// <param name="state">当前播放状态。</param>
    /// <param name="isSeveredByLongPause">是否因长暂停主动断流而等待恢复。</param>
    /// <param name="pendingResumePosition">断流时记录的待恢复位置。</param>
    internal static bool ShouldResumeViaLoadfile(MpvPlayerState state, bool isSeveredByLongPause, double? pendingResumePosition)
        => isSeveredByLongPause
            && pendingResumePosition is not null
            && state is not MpvPlayerState.Playing;

    /// <summary>
    /// loadfile 恢复完成后是否需要补 <c>ResumeAsync</c>：loadfile 会保留客户端的
    /// pause 标志，而"断流前的 paused"只是长暂停的副作用、不代表用户想要暂停——
    /// 用户点"继续"的意图就是播放，必须显式解除 pause；否则画面停在第一帧假恢复，
    /// 这个遗留 paused 还会被断流监测器当成新的长暂停再次武装计时（2026-11-16 MoAppHang 回归）。
    /// </summary>
    /// <param name="playbackState">loadfile 完成后的播放状态。</param>
    internal static bool NeedsResumeAfterLoadfile(MpvPlayerState playbackState)
        => playbackState is MpvPlayerState.Paused;
}
