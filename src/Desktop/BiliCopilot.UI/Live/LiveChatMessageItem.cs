// Copyright (c) Bili Copilot. All rights reserved.

using System.ComponentModel;

namespace BiliCopilot.UI.Live;

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
    public LiveChatMessageItem(
        LiveChatMessage message,
        Func<LiveChatSystemKind, string>? systemText,
        DateTimeOffset now)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        PriceText = message.IsSuperChat ? LiveChatSuperChatTime.FormatPrice(message.Price) : null;
        SystemText = message.Kind == LiveChatMessageKind.System
            ? systemText?.Invoke(message.SystemKind) ?? message.SystemKind.ToString()
            : null;
        BadgeText = GetBadge(message);
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

    private static string? GetBadge(LiveChatMessage message)
        => message.Kind == LiveChatMessageKind.System
            ? null
            : message.IsAdmin
                ? "管理"
                : message.GuardLevel switch
                {
                    1 => "总督",
                    2 => "提督",
                    3 => "舰长",
                    _ => message.IsVip ? "老爷" : null,
                };

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
