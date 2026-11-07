// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.MpvKernel.Core.Enums;
using Richasy.MpvKernel.Player;

namespace BiliCopilot.UI.Resolvers;

/// <summary>
/// 直播的空历史解析器：直播不读取续播位置，也不写入观看历史。
/// </summary>
/// <remarks>
/// 播放器内核在初始化/释放时按固定顺序调用历史解析器，传入空实现比传 <c>null</c>
/// 更安全，同时行为上等价于"没有历史"。
/// </remarks>
internal sealed class LiveMediaHistoryResolver : IMpvMediaHistoryResolver
{
    /// <summary>
    /// 直播永远从当前流开始，不续播。
    /// </summary>
    /// <returns>0.</returns>
    public Task<double> GetStartPositionAsync() => Task.FromResult(0d);

    /// <summary>
    /// 直播不写观看历史。
    /// </summary>
    /// <param name="position">当前位置.</param>
    /// <param name="duration">总时长.</param>
    /// <param name="state">播放状态.</param>
    /// <param name="isExiting">是否退出播放.</param>
    /// <returns>已完成的 <see cref="Task"/>.</returns>
    public Task SaveHistoryAsync(double position, double duration, MpvPlayerState state, bool isExiting = false)
        => Task.CompletedTask;
}
