// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 聊天消息类型.
/// </summary>
public enum LiveChatMessageKind
{
    /// <summary>
    /// 普通弹幕.
    /// </summary>
    Danmaku,

    /// <summary>
    /// 醒目留言.
    /// </summary>
    SuperChat,

    /// <summary>
    /// 系统消息（连接、认证、重连、下播等）.
    /// </summary>
    System,
}

/// <summary>
/// 系统消息分类，用于选择文案与图标。
/// </summary>
public enum LiveChatSystemKind
{
    /// <summary>
    /// 连接中.
    /// </summary>
    Connecting,

    /// <summary>
    /// 已连接.
    /// </summary>
    Connected,

    /// <summary>
    /// 重连中.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// 已断开.
    /// </summary>
    Disconnected,

    /// <summary>
    /// 认证失败（不重试）.
    /// </summary>
    AuthenticationFailed,

    /// <summary>
    /// 主播未开播或已下播.
    /// </summary>
    RoomNotStarted,

    /// <summary>
    /// 主播开播.
    /// </summary>
    RoomStarted,

    /// <summary>
    /// 直播被切断.
    /// </summary>
    RoomCutOff,
}

/// <summary>
/// 一条聊天消息：弹幕、醒目留言或系统提示。
/// </summary>
/// <remarks>
/// 不可变值对象，供有界缓冲与界面列表共用；不依赖 WinUI，可直接单测映射规则。
/// </remarks>
public sealed class LiveChatMessage
{
    private LiveChatMessage(LiveChatMessageKind kind, long sequence, string text)
    {
        Kind = kind;
        Sequence = sequence;
        Text = text;
    }

    /// <summary>
    /// Gets 消息类型.
    /// </summary>
    public LiveChatMessageKind Kind { get; }

    /// <summary>
    /// Gets 会话内递增序号，用于稳定排序与去重.
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// Gets 正文.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets 发送者用户 ID.
    /// </summary>
    public long UserId { get; private init; }

    /// <summary>
    /// Gets 发送者昵称.
    /// </summary>
    public string? UserName { get; private init; }

    /// <summary>
    /// Gets 舰长等级：0 无，1 总督，2 提督，3 舰长.
    /// </summary>
    public int GuardLevel { get; private init; }

    /// <summary>
    /// Gets a value indicating whether 是否为管理员.
    /// </summary>
    public bool IsAdmin { get; private init; }

    /// <summary>
    /// Gets a value indicating whether 是否为老爷/年费老爷.
    /// </summary>
    public bool IsVip { get; private init; }

    /// <summary>
    /// Gets 接收时间.
    /// </summary>
    public DateTimeOffset Timestamp { get; private init; }

    /// <summary>
    /// Gets 醒目留言标识.
    /// </summary>
    public string? SuperChatId { get; private init; }

    /// <summary>
    /// Gets 醒目留言金额（元）.
    /// </summary>
    public int Price { get; private init; }

    /// <summary>
    /// Gets 醒目留言结束时间.
    /// </summary>
    public DateTimeOffset EndTime { get; private init; }

    /// <summary>
    /// Gets 醒目留言背景色.
    /// </summary>
    public string? BackgroundColor { get; private init; }

    /// <summary>
    /// Gets 系统消息分类.
    /// </summary>
    public LiveChatSystemKind SystemKind { get; private init; }

    /// <summary>
    /// Gets 系统消息的附加说明（例如服务端原因）.
    /// </summary>
    public string? SystemDetail { get; private init; }

    /// <summary>
    /// Gets 用户等级文本（例如 UL12）.
    /// </summary>
    public string? UserLevel { get; private init; }

    /// <summary>
    /// Gets 用户等级数值.
    /// </summary>
    public int UserLevelNumber { get; private init; }

    /// <summary>
    /// Gets 用户等级颜色.
    /// </summary>
    public string? UserLevelColor { get; private init; }

    /// <summary>
    /// Gets 粉丝勋章名称.
    /// </summary>
    public string? MedalName { get; private init; }

    /// <summary>
    /// Gets 粉丝勋章等级.
    /// </summary>
    public string? MedalLevel { get; private init; }

    /// <summary>
    /// Gets 粉丝勋章颜色.
    /// </summary>
    public string? MedalColor { get; private init; }

    /// <summary>
    /// Gets 昵称颜色（装扮）.
    /// </summary>
    public string? NameColor { get; private init; }

    /// <summary>
    /// Gets 用户头衔.
    /// </summary>
    public string? UserTitle { get; private init; }

    /// <summary>
    /// Gets 头衔标识（title-233-1）；图标由头衔表映射.
    /// </summary>
    public string? UserTitleId { get; private init; }

    /// <summary>
    /// Gets 本条消息携带的表情包.
    /// </summary>
    public IReadOnlyDictionary<string, LiveDanmakuEmote>? Emotes { get; private init; }

    /// <summary>
    /// Gets 用户头像地址.
    /// </summary>
    public string? Avatar { get; private init; }

    /// <summary>
    /// Gets a value indicating whether 这是一条进房提示.
    /// </summary>
    public bool IsEnterRoom { get; private init; }

    /// <summary>
    /// Gets a value indicating whether 是否为醒目留言.
    /// </summary>
    public bool IsSuperChat => Kind == LiveChatMessageKind.SuperChat;

    /// <summary>
    /// 由一条直播弹幕创建聊天消息。
    /// </summary>
    /// <param name="danmaku">弹幕.</param>
    /// <param name="sequence">会话内序号.</param>
    /// <param name="timestamp">接收时间.</param>
    /// <returns>聊天消息.</returns>
    public static LiveChatMessage FromDanmaku(LiveDanmakuInformation danmaku, long sequence, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(danmaku);
        return new LiveChatMessage(LiveChatMessageKind.Danmaku, sequence, danmaku.Text ?? string.Empty)
        {
            UserId = danmaku.UserId,
            UserName = danmaku.UserName,
            GuardLevel = danmaku.GuardLevel,
            IsAdmin = danmaku.IsAdmin,
            IsVip = danmaku.IsVip,
            UserLevel = danmaku.UserLevel,
            UserLevelNumber = danmaku.UserLevelNumber,
            UserLevelColor = danmaku.UserLevelColor,
            MedalName = danmaku.MedalName,
            MedalLevel = danmaku.MedalLevel,
            MedalColor = danmaku.MedalColor,
            NameColor = danmaku.NameColor,
            UserTitle = danmaku.UserTitle,
            UserTitleId = danmaku.UserTitleId,
            Emotes = danmaku.Emotes,
            Avatar = danmaku.UserFace,
            Timestamp = timestamp,
        };
    }

    /// <summary>
    /// 由一条进房事件创建聊天消息；作为普通列表项追加到最下面。
    /// </summary>
    /// <param name="user">进入用户.</param>
    /// <param name="sequence">会话内序号.</param>
    /// <param name="timestamp">接收时间.</param>
    /// <returns>聊天消息.</returns>
    public static LiveChatMessage FromEnter(LiveUserEnteredInformation user, long sequence, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new LiveChatMessage(LiveChatMessageKind.System, sequence, string.Empty)
        {
            UserId = user.UserId,
            UserName = user.UserName,
            GuardLevel = user.GuardLevel,
            MedalName = user.MedalName,
            MedalLevel = user.MedalLevel,
            MedalColor = user.MedalColor,
            NameColor = user.NameColor,
            IsAdmin = user.IsAdmin,
            IsVip = user.IsVip,
            IsEnterRoom = true,
            Timestamp = timestamp,
        };
    }

    /// <summary>
    /// 由一条醒目留言创建聊天消息。
    /// </summary>
    /// <param name="superChat">醒目留言.</param>
    /// <param name="sequence">会话内序号.</param>
    /// <returns>聊天消息.</returns>
    public static LiveChatMessage FromSuperChat(LiveSuperChatInformation superChat, long sequence)
    {
        ArgumentNullException.ThrowIfNull(superChat);
        return new LiveChatMessage(LiveChatMessageKind.SuperChat, sequence, superChat.Message ?? string.Empty)
        {
            UserId = superChat.UserId,
            UserName = superChat.UserName,
            Timestamp = superChat.StartTime,
            SuperChatId = superChat.Id,
            Price = superChat.Price,
            EndTime = superChat.EndTime,
            BackgroundColor = superChat.BackgroundColor,
        };
    }

    /// <summary>
    /// 创建一条系统消息。
    /// </summary>
    /// <param name="kind">系统消息分类.</param>
    /// <param name="sequence">会话内序号.</param>
    /// <param name="timestamp">发生时间.</param>
    /// <param name="detail">附加说明.</param>
    /// <returns>聊天消息.</returns>
    public static LiveChatMessage System(
        LiveChatSystemKind kind,
        long sequence,
        DateTimeOffset timestamp,
        string? detail = null)
        => new(LiveChatMessageKind.System, sequence, string.Empty)
        {
            SystemKind = kind,
            SystemDetail = string.IsNullOrWhiteSpace(detail) ? null : detail,
            Timestamp = timestamp,
        };
}
