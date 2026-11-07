// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 系统媒体控制按钮映射出的播放器动作。
/// </summary>
internal enum SmtcPlayerAction
{
    /// <summary>
    /// 未映射的按钮。
    /// </summary>
    None,

    /// <summary>
    /// 播放/暂停。
    /// </summary>
    PlayPause,

    /// <summary>
    /// 下一集。
    /// </summary>
    Next,

    /// <summary>
    /// 上一集。
    /// </summary>
    Previous,
}

/// <summary>
/// 播放初始化回调中会触碰播放器/客户端的步骤。
/// </summary>
internal sealed class PlaybackInitializationSteps
{
    /// <summary>
    /// Gets 应用字幕字体与字号。
    /// </summary>
    public required Func<Task> ApplySubtitlesAsync { get; init; }

    /// <summary>
    /// Gets 跳转到上次播放位置。
    /// </summary>
    public required Func<Task> SeekToContinuePositionAsync { get; init; }

    /// <summary>
    /// Gets 关闭统计信息覆盖层。
    /// </summary>
    public required Func<Task> CloseStatsOverlayAsync { get; init; }

    /// <summary>
    /// Gets 确保媒体处于播放状态。
    /// </summary>
    public required Func<Task> ResumeIfNeededAsync { get; init; }

    /// <summary>
    /// Gets 校验章节信息。
    /// </summary>
    public required Func<Task> VerifyChaptersAsync { get; init; }

    /// <summary>
    /// Gets 初始化弹幕。
    /// </summary>
    public required Action InitializeDanmaku { get; init; }
}

public sealed partial class PlayerViewModel
{
    /// <summary>
    /// 执行播放初始化后的步骤；每个 await 之后重新验证会话，失效即停止。
    /// </summary>
    /// <param name="isCurrent">处理事件前捕获的会话有效性。</param>
    /// <param name="steps">初始化步骤。</param>
    /// <returns><see cref="Task"/>。</returns>
    internal static async Task RunPlaybackInitializationAsync(Func<bool> isCurrent, PlaybackInitializationSteps steps)
    {
        ArgumentNullException.ThrowIfNull(isCurrent);
        ArgumentNullException.ThrowIfNull(steps);

        if (!isCurrent())
        {
            return;
        }

        await steps.ApplySubtitlesAsync();
        if (!isCurrent())
        {
            return;
        }

        await steps.SeekToContinuePositionAsync();
        if (!isCurrent())
        {
            return;
        }

        await steps.CloseStatsOverlayAsync();
        if (!isCurrent())
        {
            return;
        }

        await steps.ResumeIfNeededAsync();
        if (!isCurrent())
        {
            return;
        }

        await steps.VerifyChaptersAsync();
        if (!isCurrent())
        {
            return;
        }

        steps.InitializeDanmaku();
    }

    /// <summary>
    /// 执行已入队的系统媒体控制动作。
    /// 入队后会话可能已关闭或换集，必须用入队前捕获的有效性复查，失效则不做任何播放器操作。
    /// </summary>
    /// <param name="action">按钮对应的动作。</param>
    /// <param name="isCurrent">入队前捕获的会话有效性。</param>
    internal void RunSmtcAction(SmtcPlayerAction action, Func<bool>? isCurrent)
    {
        if (action == SmtcPlayerAction.None || isCurrent is null || !isCurrent())
        {
            return;
        }

        ExecuteSmtcPlayerAction(action);
    }

    /// <summary>
    /// 换集的暂停/停止/初始化序列。
    /// 每个 await 之后重新验证会话：关闭、换集或替换实例后不再继续。
    /// </summary>
    /// <param name="pause">暂停旧媒体。</param>
    /// <param name="stop">停止旧媒体。</param>
    /// <param name="initialize">初始化新媒体。</param>
    /// <returns><see cref="Task"/>。</returns>
    internal async Task SwitchMediaAsync(Func<Task> pause, Func<Task> stop, Func<Task> initialize)
    {
        var isCurrent = CapturePlaybackValidity();
        if (!isCurrent())
        {
            return;
        }

        CancelPlaybackRefresh();
        // 先让已接纳的跳转落定，避免暂停/停止与旧写入交叉。
        await WaitForPositionChangesAsync();
        if (!isCurrent())
        {
            return;
        }

        await pause();
        if (!isCurrent())
        {
            return;
        }

        await stop();
        if (!isCurrent())
        {
            return;
        }

        await initialize();
    }

    /// <summary>
    /// 换清晰度后重播当前位置。
    /// 播放器已被关闭或替换时不得再写旧实例。
    /// </summary>
    /// <param name="isCurrent">换清晰度前捕获的会话有效性。</param>
    /// <returns><see cref="Task"/>。</returns>
    internal async Task ReplayCurrentPositionAsync(Func<bool> isCurrent)
    {
        if (!isCurrent())
        {
            return;
        }

        var player = Player;
        if (player is null)
        {
            return;
        }

        var position = player.Position;
        if (!isCurrent())
        {
            return;
        }

        await player.ReplayAsync(position);
    }

    /// <summary>
    /// 执行真实播放器命令；测试工程用适配器记录动作，避免依赖 WinUI 命令。
    /// </summary>
    /// <param name="action">按钮对应的动作。</param>
    partial void ExecuteSmtcPlayerAction(SmtcPlayerAction action);
}
