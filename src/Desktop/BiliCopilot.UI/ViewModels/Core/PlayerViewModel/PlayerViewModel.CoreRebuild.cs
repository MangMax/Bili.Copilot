// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.Extensions.Logging;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 播放核心死亡后的完整重建（#228）。
/// </summary>
public sealed partial class PlayerViewModel
{
    /// <summary>
    /// 死核心重建：「重新加载」/刷新按钮与错误操作按钮共用的入口。
    /// 先让旧 <see cref="Player"/>/<see cref="Client"/> 走到 <c>IsDisposed</c>，
    /// 再重新进入 <see cref="InitializeAsync"/> 的 <c>Client?.IsDisposed != false</c>
    /// 分支完整重建 <c>MpvClient</c>+<c>MpvPlayer</c>+<c>MpvMediaDisplaySource</c>，
    /// 闸门在该分支里复位（#225），<c>{x:Bind ViewModel.Player}</c> 随新实例重挂。
    /// </summary>
    private async partial Task RebuildPlaybackCoreAsync()
    {
        if (_snapshot is null || Session.IsClosing || Session.IsReleased || !IsSessionCurrent())
        {
            return;
        }

        var snapshot = _snapshot;
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("播放核心已退出，开始重建 MpvClient+MpvPlayer：{Media}", DescribeSnapshot(snapshot));
        }

        await _coreRebuild.RebuildAsync(
            cancelPending: () =>
            {
                // 断流计时与刷新准入都指向死句柄，先取消再拆。
                _longPauseSeverMonitor?.CancelTimer();
                CancelPlaybackRefresh();
            },
            disposePlayerAsync: DisposeDeadPlayerAsync,
            disposeClientAsync: DisposeDeadClientAsync,
            reinitializeAsync: () => InitializeAsync(snapshot));

        RefreshPlaybackCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 解绑并释放死 <see cref="Player"/>：先把 <see cref="Player"/> 置空让迟到回调
    /// （<c>ReferenceEquals(sender, Player)</c> 复查）落到新会话/空引用上，
    /// 再退订事件并 <c>DisposeAsync</c>——它先写历史再释放绑定的 client。
    /// </summary>
    private async Task DisposeDeadPlayerAsync()
    {
        var player = Player;
        Player = null!;
        if (player is null)
        {
            return;
        }

        player.PropertyChanged -= OnPlayerPropertyChanged;
        player.PropertyChanged -= OnDanmakuPlayerPropertyChanged;
        try
        {
            await player.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "释放已退出的播放核心（MpvPlayer）时失败");
        }
    }

    /// <summary>
    /// 释放死 <see cref="Client"/>：<c>MpvPlayer.DisposeAsync</c> 可能已顺手释放，
    /// 这里用 <c>IsDisposed</c> 判空，保证不重复释放。
    /// </summary>
    private async Task DisposeDeadClientAsync()
    {
        if (Client?.IsDisposed == false)
        {
            try
            {
                await Client.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "释放已退出的播放核心（MpvClient）时失败");
            }
        }
    }
}
