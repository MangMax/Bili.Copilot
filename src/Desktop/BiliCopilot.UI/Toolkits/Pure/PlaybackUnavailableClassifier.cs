// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Bili.Media;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 播放不可用时展示给用户的消息种类.
/// </summary>
public enum PlaybackUnavailableMessage
{
    /// <summary>
    /// 需要登录.
    /// </summary>
    LoginRequired,

    /// <summary>
    /// 需要大会员或购买.
    /// </summary>
    NeedVip,

    /// <summary>
    /// 登录状态与会员权益不一致（看起来已登录但没有权益）.
    /// </summary>
    SessionExpired,

    /// <summary>
    /// 地区限制.
    /// </summary>
    RegionRestricted,

    /// <summary>
    /// 内容不可用.
    /// </summary>
    ContentUnavailable,

    /// <summary>
    /// 获取播放地址失败（网络或解析问题）.
    /// </summary>
    SourceFailed,
}

/// <summary>
/// 播放不可用时可以提供给用户的操作.
/// </summary>
public enum PlaybackUnavailableAction
{
    /// <summary>
    /// 只提示，不提供操作.
    /// </summary>
    None,

    /// <summary>
    /// 重新登录以同步会话.
    /// </summary>
    ReSignIn,

    /// <summary>
    /// 前往大会员页面.
    /// </summary>
    OpenVipPage,
}

/// <summary>
/// 播放不可用的提示内容.
/// </summary>
/// <param name="Message">消息种类.</param>
/// <param name="Action">可执行操作的种类.</param>
public readonly record struct PlaybackUnavailableNotice(PlaybackUnavailableMessage Message, PlaybackUnavailableAction Action);

/// <summary>
/// 把内核给出的播放不可用原因翻译成界面提示与操作.
/// </summary>
/// <remarks>
/// 只看内核判定出的原因与当前账号是否显示为大会员，不猜测服务端权益.
/// </remarks>
public static class PlaybackUnavailableClassifier
{
    /// <summary>
    /// 判定提示内容.
    /// </summary>
    /// <param name="reason">内核给出的原因；为空表示普通失败（网络/解析）. </param>
    /// <param name="isVip">当前账号是否显示为大会员.</param>
    /// <returns>提示与操作.</returns>
    public static PlaybackUnavailableNotice Classify(PgcPlaybackUnavailableReason? reason, bool isVip)
        => reason switch
        {
            PgcPlaybackUnavailableReason.LoginRequired
                => new(PlaybackUnavailableMessage.LoginRequired, PlaybackUnavailableAction.ReSignIn),
            PgcPlaybackUnavailableReason.NotEntitled when isVip
                => new(PlaybackUnavailableMessage.SessionExpired, PlaybackUnavailableAction.ReSignIn),
            PgcPlaybackUnavailableReason.NotEntitled
                => new(PlaybackUnavailableMessage.NeedVip, PlaybackUnavailableAction.OpenVipPage),
            PgcPlaybackUnavailableReason.RegionRestricted
                => new(PlaybackUnavailableMessage.RegionRestricted, PlaybackUnavailableAction.None),
            PgcPlaybackUnavailableReason.ContentUnavailable
                => new(PlaybackUnavailableMessage.ContentUnavailable, PlaybackUnavailableAction.None),
            _ => new(PlaybackUnavailableMessage.SourceFailed, PlaybackUnavailableAction.None),
        };
}