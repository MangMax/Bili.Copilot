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
    /// 把播放器内核错误翻译成纯逻辑层的错误替身。
    /// </summary>
    /// <param name="error">播放器错误.</param>
    /// <returns>错误替身.</returns>
    public static LivePlayerError FromPlayerError(MpvError error)
        => error switch
        {
            MpvError.Http403 => LivePlayerError.Http403,
            MpvError.Http404 => LivePlayerError.Http404,
            // 内核的 MpvError 目前没有 412 成员（只有 Http403/Http404/Http500），
            // 所以 LivePlayerError.Http412 只能由取址阶段的异常分类产生；
            // 内核补上成员后在这里加一行即可。
            MpvError.Http500 => LivePlayerError.HttpServerError,
            MpvError.TlsError => LivePlayerError.Tls,
            MpvError.LoadingFailed => LivePlayerError.LoadingFailed,
            MpvError.VoInitFailed => LivePlayerError.VideoInitFailed,
            MpvError.AoInitFailed => LivePlayerError.AudioInitFailed,
            MpvError.Unsupported => LivePlayerError.Unsupported,
            MpvError.UnknownFormat => LivePlayerError.UnknownFormat,
            MpvError.NothingToPlay => LivePlayerError.NothingToPlay,
            _ => LivePlayerError.Other,
        };
}