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
}
