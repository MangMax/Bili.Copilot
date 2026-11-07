// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class PlayerViewModel
{
    /// <summary>
    /// 让视图模型立即失效：关闭、释放与初始化失败回滚都从这里开始。
    /// 只做幂等的状态失效，重复调用安全。
    /// </summary>
    internal void InvalidateForRelease()
    {
        // 先拒绝迟到回调，再取消弹幕加载；之后的回调都会被 CapturePlaybackValidity 拦下。
        _isDanmakuClosed = true;
        CancelPlaybackRefresh();
        _danmakuLoadSession?.Dispose();
    }

    /// <summary>
    /// 当前会话是否仍然有效；用于点击等不需要“先捕获、await 后复查”的同步入口。
    /// </summary>
    /// <returns>会话仍是当前会话时为 true。</returns>
    internal bool IsSessionCurrent() => CapturePlaybackValidity()();

    /// <summary>
    /// 释放路径：先失效视图模型，再等已接纳的写入落定，最后释放原生资源。
    /// 释放闸门保证它只会执行一次，但失效本身是幂等的。
    /// </summary>
    /// <param name="releaseResources">释放会话创建的原生资源。</param>
    /// <returns><see cref="Task"/>。</returns>
    internal async Task ReleaseSessionCoreAsync(Func<Task> releaseResources)
    {
        ArgumentNullException.ThrowIfNull(releaseResources);
        InvalidateForRelease();
        // 释放前等已接纳的调速与跳转落定，避免它们打到已销毁的客户端。
#pragma warning disable VSTHRD003
        await WaitForSpeedChangesAsync();
        await WaitForPositionChangesAsync();
#pragma warning restore VSTHRD003
        await releaseResources();
    }
}
