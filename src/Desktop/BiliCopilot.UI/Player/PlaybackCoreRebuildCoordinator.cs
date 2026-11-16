// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Player;

/// <summary>
/// 死核心的完整重建编排（#228）：固定顺序为
/// 取消未决操作 → 解绑并释放 <c>MpvPlayer</c> → 释放 <c>MpvClient</c> → 重新初始化。
/// <para>
/// <c>MpvPlayer</c> 必须先于 <c>MpvClient</c> 释放：<c>MpvPlayer.DisposeAsync</c>
/// 先写历史再释放它绑定的 client，顺序反了会丢掉最后一刻的进度。
/// </para>
/// 重建是单飞的：进行中再次请求汇入同一任务，teardown 与重建各只执行一次；
/// 完成后的下一次重建（核心可能再次 Shutdown）重新走完整流程。
/// </summary>
internal sealed class PlaybackCoreRebuildCoordinator
{
    private Task _activeRebuild = Task.CompletedTask;

    /// <summary>
    /// 当前是否有进行中的重建。
    /// </summary>
    public bool IsRebuilding => !_activeRebuild.IsCompleted;

    /// <summary>
    /// 重建汇合点：释放路径在最终释放原生资源前等它落停，
    /// 避免与重建争抢同一批 player/client 句柄。
    /// </summary>
#pragma warning disable VSTHRD003 // 汇合点语义就是有意识地等待别处启动的重建任务。
    public Task Drained => _activeRebuild;
#pragma warning restore VSTHRD003

    /// <summary>
    /// 死核心重建的唯一入口。
    /// </summary>
    /// <param name="cancelPending">取消未决操作（长暂停断流计时、进行中的刷新准入）。</param>
    /// <param name="disposePlayerAsync">解绑事件并释放 <c>MpvPlayer</c>；先写历史再释放其 client。</param>
    /// <param name="disposeClientAsync">释放 <c>MpvClient</c>；player 已释放 client 时由 <c>IsDisposed</c> 判空跳过。</param>
    /// <param name="reinitializeAsync">重新进入 <c>InitializeAsync</c> 的 <c>Client?.IsDisposed != false</c> 分支完整重建。</param>
    /// <returns>本次重建任务；并发的调用汇入同一任务。</returns>
    public Task RebuildAsync(
        Action cancelPending,
        Func<Task> disposePlayerAsync,
        Func<Task> disposeClientAsync,
        Func<Task> reinitializeAsync)
    {
        ArgumentNullException.ThrowIfNull(cancelPending);
        ArgumentNullException.ThrowIfNull(disposePlayerAsync);
        ArgumentNullException.ThrowIfNull(disposeClientAsync);
        ArgumentNullException.ThrowIfNull(reinitializeAsync);

        if (IsRebuilding)
        {
            return _activeRebuild;
        }

        var rebuild = RunAsync(cancelPending, disposePlayerAsync, disposeClientAsync, reinitializeAsync);
        _activeRebuild = rebuild;
        return rebuild;
    }

    private static async Task RunAsync(
        Action cancelPending,
        Func<Task> disposePlayerAsync,
        Func<Task> disposeClientAsync,
        Func<Task> reinitializeAsync)
    {
        cancelPending();
        await disposePlayerAsync();
        await disposeClientAsync();
        await reinitializeAsync();
    }
}
