// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;
using Richasy.MpvKernel;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播失败/状态文案：把纯逻辑的失败分类翻译成资源键。
/// </summary>
internal static class LiveFailureText
{
    /// <summary>
    /// 获取失败分类对应的提示资源。
    /// </summary>
    /// <param name="kind">失败分类.</param>
    /// <returns>资源键.</returns>
    public static StringNames GetMessage(LiveFailureKind kind)
        => kind switch
        {
            LiveFailureKind.AuthenticationFailed => StringNames.LivePlaybackAuthenticationFailed,
            LiveFailureKind.DecodeFailed => StringNames.LivePlaybackDecodeFailed,
            LiveFailureKind.AddressUnavailable => StringNames.LivePlaybackAddressFailed,
            _ => StringNames.LivePlaybackFailed,
        };

    /// <summary>
    /// 获取房间状态资源。
    /// </summary>
    /// <param name="isLiving">是否正在直播.</param>
    /// <param name="isLoading">详情是否仍在加载.</param>
    /// <returns>资源键.</returns>
    public static StringNames GetRoomStatus(bool isLiving, bool isLoading)
        => isLoading ? StringNames.Connecting : isLiving ? StringNames.LiveRoomLiving : StringNames.LiveRoomNotLiving;

    /// <summary>
    /// 把播放器内核错误翻译成失败分类。
    /// </summary>
    /// <param name="error">播放器错误.</param>
    /// <returns>失败分类.</returns>
    public static LiveFailureKind FromMpvError(MpvError error)
        => error switch
        {
            MpvError.VoInitFailed or MpvError.Unsupported or MpvError.UnknownFormat => LiveFailureKind.DecodeFailed,
            _ => LiveFailureKind.AddressUnavailable,
        };
}
