// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降片段异步加载结果的落地判定.
/// </summary>
/// <remarks>
/// 加载完成时既要确认请求没有被后续的切换视频 / 分 P 取代（generation），
/// 也要确认用户没有在请求期间关闭总开关.
/// </remarks>
public static class SponsorBlockLoadGuard
{
    /// <summary>
    /// 判断一次加载结果是否仍可写入当前视图模型.
    /// </summary>
    /// <param name="generation">发起请求时的代次.</param>
    /// <param name="currentGeneration">当前代次.</param>
    /// <param name="isEnabled">空降助手总开关当前是否开启.</param>
    /// <returns>可以应用结果时返回 <c>true</c>.</returns>
    public static bool ShouldApplyResult(int generation, int currentGeneration, bool isEnabled)
        => isEnabled && generation == currentGeneration;
}
