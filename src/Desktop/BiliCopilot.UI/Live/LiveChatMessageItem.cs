// Copyright (c) Bili Copilot. All rights reserved.

using System.ComponentModel;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 聊天徽标种类；只决定展示文案，不影响准入或屏蔽。
/// </summary>
public enum LiveChatBadgeKind
{
    /// <summary>管理员.</summary>
    Admin,

    /// <summary>总督（内核 GuardLevel = 1）.</summary>
    Governor,

    /// <summary>提督（内核 GuardLevel = 2）.</summary>
    Admiral,

    /// <summary>舰长（内核 GuardLevel = 3）.</summary>
    Captain,

    /// <summary>老爷/年费老爷.</summary>
    Vip,
}

/// <summary>
/// 徽标的内置中文文案：纯逻辑层与单测的兜底，界面注入本地化解析器覆盖它。
/// </summary>
public static class LiveChatBadgeKindText
{
    /// <summary>
    /// 获取内置文案。
    /// </summary>
    /// <param name="kind">徽标种类.</param>
    /// <returns>展示文案.</returns>
    public static string Get(LiveChatBadgeKind kind)
        => kind switch
        {
            LiveChatBadgeKind.Admin => "管理",
            LiveChatBadgeKind.Governor => "总督",
            LiveChatBadgeKind.Admiral => "提督",
            LiveChatBadgeKind.Captain => "舰长",
            _ => "老爷",
        };
}

/// <summary>
/// 聊天列表里的一个条目：把不可变的 <see cref="LiveChatMessage"/> 映射成界面可直接绑定的展示字段。
/// </summary>
/// <remarks>
/// 只有醒目留言的剩余时间会变化，因此这里手写一个最小 <see cref="INotifyPropertyChanged"/>，
/// 由列表控件按固定节拍调用 <see cref="UpdateRemaining"/>，而不是每个条目自带计时器。
/// 系统文案由调用方注入的解析器决定，纯逻辑层不依赖资源键。
/// </remarks>
public sealed class LiveChatMessageItem : INotifyPropertyChanged
{
    private string? _remainingText;
    private bool _isRemainingVisible;
    private bool _isExpired;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatMessageItem"/> class.
    /// </summary>
    /// <param name="message">原始消息.</param>
    /// <param name="systemText">系统消息文案解析器；为空时回落到枚举名.</param>
    /// <param name="now">创建时间，用于计算醒目留言剩余时间.</param>
    /// <param name="badgeText">徽标文案解析器；为空时回落到内置中文，便于纯逻辑单测.</param>
    /// <param name="priceFormat">金额格式解析器；为空时回落到内置 <c>¥</c>.</param>
    public LiveChatMessageItem(
        LiveChatMessage message,
        Func<LiveChatSystemKind, string>? systemText,
        DateTimeOffset now,
        Func<LiveChatBadgeKind, string>? badgeText = null,
        Func<int, string>? priceFormat = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        PriceText = message.IsSuperChat
            ? (priceFormat ?? LiveChatSuperChatTime.FormatPrice)(message.Price)
            : null;
        SystemText = message.Kind == LiveChatMessageKind.System
            ? systemText?.Invoke(message.SystemKind) ?? message.SystemKind.ToString()
            : null;
        BadgeText = GetBadge(message, badgeText ?? LiveChatBadgeKindText.Get);
        Segments = LiveChatTextSegment.Parse(message.Text, message.Emotes);
        Content = new Richasy.BiliKernel.Models.Appearance.EmoteText(
            message.Text ?? string.Empty,
            message.Emotes?.ToDictionary(
                pair => $"[{pair.Key}]",
                pair => new Richasy.BiliKernel.Models.Appearance.BiliImage(new Uri(pair.Value.Url))));
        UpdateRemaining(now);
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets 原始消息.
    /// </summary>
    public LiveChatMessage Message { get; }

    /// <summary>
    /// Gets 消息类型.
    /// </summary>
    public LiveChatMessageKind Kind => Message.Kind;

    /// <summary>
    /// Gets 正文.
    /// </summary>
    public string Text => Message.Text;

    /// <summary>
    /// Gets 发送者昵称.
    /// </summary>
    public string? UserName => Message.UserName;

    /// <summary>
    /// Gets 用户等级（含 UL 前缀）.
    /// </summary>
    public string? UserLevel => Message.UserLevel;

    /// <summary>
    /// Gets 用户等级数值，供网页同款等级图使用.
    /// </summary>
    public int UserLevelNumber => Message.UserLevelNumber;

    /// <summary>
    /// Gets a value indicating whether 有等级徽标.
    /// </summary>
    public bool HasUserLevel => !string.IsNullOrEmpty(Message.UserLevel);

    /// <summary>
    /// Gets 用户等级颜色.
    /// </summary>
    public string? UserLevelColor => Message.UserLevelColor;

    /// <summary>
    /// Gets 粉丝勋章名称.
    /// </summary>
    public string? MedalName => Message.MedalName;

    /// <summary>
    /// Gets a value indicating whether 有粉丝勋章.
    /// </summary>
    public bool HasMedal => !string.IsNullOrEmpty(Message.MedalName);

    /// <summary>
    /// Gets 粉丝勋章等级.
    /// </summary>
    public string? MedalLevel => Message.MedalLevel;

    /// <summary>
    /// Gets 粉丝勋章颜色.
    /// </summary>
    public string? MedalColor => Message.MedalColor;

    /// <summary>
    /// Gets 昵称颜色（装扮）.
    /// </summary>
    public string? NameColor => Message.NameColor;

    /// <summary>
    /// Gets 用户头衔.
    /// </summary>
    public string? UserTitle => Message.UserTitle;

    /// <summary>
    /// Gets a value indicating whether 是进房提示.
    /// </summary>
    public bool IsEnterRoom => Message.IsEnterRoom;

    /// <summary>
    /// Gets a value indicating whether 渲染为普通聊天卡片.
    /// </summary>
    public bool ShowNormalRow => !IsSuperChat && !IsEnterRoom;

    /// <summary>
    /// Gets 进房提示文案.
    /// </summary>
    public string? EnterText => Message.IsEnterRoom ? $"{Message.UserName} 进入直播间" : null;

    /// <summary>
    /// Gets 正文分段：普通文本与表情图片.
    /// </summary>
    public IReadOnlyList<LiveChatTextSegment> Segments { get; } = Array.Empty<LiveChatTextSegment>();

    /// <summary>
    /// Gets 供 <c>EmoteTextBlock</c> 使用的富文本：文本 + 表情索引，和点播评论同一套渲染.
    /// </summary>
    public Richasy.BiliKernel.Models.Appearance.EmoteText Content { get; }

    /// <summary>
    /// Gets 头像地址.
    /// </summary>
    public string? Avatar => Message.Avatar;

    /// <summary>
    /// Gets 头像图片；没有头像时为 null.
    /// </summary>
    public Uri? AvatarUri
        => Uri.TryCreate(Message.Avatar, UriKind.Absolute, out var uri) ? uri : null;

    /// <summary>
    /// Gets 昵称画刷（装扮颜色）；没有时回落到默认.
    /// </summary>
    public Brush? NameBrush => CreateBrush(Message.NameColor);

    /// <summary>
    /// Gets 勋章画刷.
    /// </summary>
    public Brush? MedalBrush => CreateBrush(Message.MedalColor);

    /// <summary>
    /// Gets 等级画刷.
    /// </summary>
    public Brush? UserLevelBrush => CreateBrush(Message.UserLevelColor);

    /// <summary>
    /// Gets a value indicating whether 有任意徽标需要显示.
    /// </summary>
    public bool HasAnyBadge => HasMedal || HasUserLevel || !string.IsNullOrEmpty(BadgeText) || HasTitle;

    /// <summary>
    /// Gets a value indicating whether 有头衔.
    /// </summary>
    public bool HasTitle => !string.IsNullOrEmpty(Message.UserTitle);

    /// <summary>
    /// Gets 昵称徽标（舰长/管理/老爷）；没有徽标时为 null.
    /// </summary>
    public string? BadgeText { get; }

    /// <summary>
    /// Gets 金额文本；非醒目留言为 null.
    /// </summary>
    public string? PriceText { get; }

    /// <summary>
    /// Gets 系统消息文案；非系统消息为 null.
    /// </summary>
    public string? SystemText { get; }

    /// <summary>
    /// Gets 系统消息附加说明.
    /// </summary>
    public string? SystemDetail => Message.SystemDetail;

    /// <summary>
    /// Gets 剩余时间文本；非醒目留言或已结束时为 null.
    /// </summary>
    public string? RemainingText
    {
        get => _remainingText;
        private set
        {
            if (_remainingText == value)
            {
                return;
            }

            _remainingText = value;
            Raise(nameof(RemainingText));
        }
    }

    /// <summary>
    /// Gets a value indicating whether 是否显示剩余时间.
    /// </summary>
    public bool IsRemainingVisible
    {
        get => _isRemainingVisible;
        private set
        {
            if (_isRemainingVisible == value)
            {
                return;
            }

            _isRemainingVisible = value;
            Raise(nameof(IsRemainingVisible));
        }
    }

    /// <summary>
    /// Gets a value indicating whether 是否为醒目留言.
    /// </summary>
    public bool IsSuperChat => Message.IsSuperChat;

    /// <summary>
    /// Gets a value indicating whether 是否为系统消息.
    /// </summary>
    public bool IsSystem => Message.Kind == LiveChatMessageKind.System;

    /// <summary>
    /// Gets a value indicating whether 是否为普通弹幕.
    /// </summary>
    public bool IsDanmaku => Message.Kind == LiveChatMessageKind.Danmaku;

    /// <summary>
    /// Gets a value indicating whether 醒目留言已经过期.
    /// </summary>
    public bool IsExpired
    {
        get => _isExpired;
        private set
        {
            if (_isExpired == value)
            {
                return;
            }

            _isExpired = value;
            Raise(nameof(IsExpired));
        }
    }

    /// <summary>
    /// Gets 醒目留言背景色.
    /// </summary>
    public string? BackgroundColor => Message.BackgroundColor;

    /// <summary>
    /// 按当前时间刷新剩余时间。
    /// </summary>
    /// <param name="now">当前时间.</param>
    public void UpdateRemaining(DateTimeOffset now)
    {
        if (!Message.IsSuperChat)
        {
            RemainingText = null;
            IsRemainingVisible = false;
            IsExpired = false;
            return;
        }

        var expired = LiveChatSuperChatTime.IsExpired(Message.EndTime, now);
        IsExpired = expired;
        IsRemainingVisible = !expired;
        RemainingText = expired
            ? null
            : LiveChatSuperChatTime.FormatRemaining(LiveChatSuperChatTime.Remaining(Message.EndTime, now));
    }

    private static string? GetBadge(LiveChatMessage message, Func<LiveChatBadgeKind, string> text)
        => message.Kind == LiveChatMessageKind.System
            ? null
            : message.IsAdmin
                ? text(LiveChatBadgeKind.Admin)
                : message.GuardLevel switch
                {
                    1 => text(LiveChatBadgeKind.Governor),
                    2 => text(LiveChatBadgeKind.Admiral),
                    3 => text(LiveChatBadgeKind.Captain),
                    _ => message.IsVip ? text(LiveChatBadgeKind.Vip) : null,
                };

    private static Brush? CreateBrush(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        try
        {
            return new SolidColorBrush(AppToolkit.HexToColor(color));
        }
        catch
        {
            return null;
        }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
