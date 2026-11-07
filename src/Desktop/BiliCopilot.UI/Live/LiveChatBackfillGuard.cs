// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 补屏结果的有效性判定：迟到的补屏只能落在发起它的那一次观看上。
/// </summary>
/// <remarks>
/// 补屏在后台线程完成，回到 UI 线程时房间可能已经换了。判定必须同时匹配
/// "聊天归属的代际"与"会话的代际"：聊天代际由批量节拍同步，在换房后的第一次节拍之前
/// 仍是旧值，因此只看其中一个会让旧房间的弹幕落进新房间的列表。
/// 纯逻辑，不读取任何状态，便于用假代际直接测试。
/// </remarks>
public static class LiveChatBackfillGuard
{
    /// <summary>
    /// 判断一次补屏是否仍属于当前观看。
    /// </summary>
    /// <param name="generation">发起补屏时的代际.</param>
    /// <param name="chatGeneration">聊天列表当前的代际.</param>
    /// <param name="sessionGeneration">直播会话当前的代际.</param>
    /// <returns>仍然有效时为 true.</returns>
    public static bool IsCurrent(int generation, int chatGeneration, int sessionGeneration)
        => generation == chatGeneration && generation == sessionGeneration;
}
