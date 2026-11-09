// Copyright (c) Bili Copilot. All rights reserved.

using System.ComponentModel;
using System.Text.RegularExpressions;
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
public sealed partial class LiveChatMessageItem : INotifyPropertyChanged
{
    private readonly Brush? _nameBrush;
    private readonly Brush? _medalBrush;
    private readonly Brush? _medalBackgroundBrush;
    private readonly Brush? _medalGradientBrush;
    private readonly Brush? _medalTextBrush;
    private int _mergeCount = 1;
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
    /// <param name="emoteResources">直播间表情包索引；用于把官方表情的 official_xxx 映射回 [名称].</param>
    /// <param name="titleResources">直播头衔索引；用于把 title-233-1 映射成图标.</param>
    public LiveChatMessageItem(
        LiveChatMessage message,
        Func<LiveChatSystemKind, string>? systemText,
        DateTimeOffset now,
        Func<LiveChatBadgeKind, string>? badgeText = null,
        Func<int, string>? priceFormat = null,
        IReadOnlyDictionary<string, Richasy.BiliKernel.Models.Media.LiveDanmakuEmote>? emoteResources = null,
        IReadOnlyDictionary<string, Richasy.BiliKernel.Models.Media.LiveTitleItem>? titleResources = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        PriceText = message.IsSuperChat
            ? (priceFormat ?? LiveChatSuperChatTime.FormatPrice)(message.Price)
            : null;
        SystemText = message.Kind == LiveChatMessageKind.System
            ? systemText?.Invoke(message.SystemKind) ?? message.SystemKind.ToString()
            : null;
        var badge = GetBadge(message, badgeText ?? LiveChatBadgeKindText.Get);
        _nameBrush = CreateBrush(message.NameColor);
        _medalBrush = CreateBrush(message.MedalColor);
        _medalBackgroundBrush = CreateTintBrush(message.MedalColor, 0x2E);
        _medalGradientBrush = CreateGradientBrush(message.MedalColorStart, message.MedalColorEnd) ?? _medalBackgroundBrush;
        _medalTextBrush = CreateBrush(message.MedalTextColor) ?? _medalBrush;
        MedalGuardIconSource = CreateBitmap(message.MedalGuardIcon, 32);
        // 船锚已经在勋章里表达了大航海身份，不再重复挂“舰长”文字标签。
        BadgeText = message.GuardLevel > 0 && MedalGuardIconSource is not null ? null : badge;
        TitleIconSource = BuildTitleIcon(message.UserTitleId, titleResources);
        Content = new Richasy.BiliKernel.Models.Appearance.EmoteText(
            message.Text ?? string.Empty,
            BuildEmoteIndex(message.Text, message.Emotes, emoteResources));
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
    /// Gets a value indicating whether 是进房提示.
    /// </summary>
    public bool IsEnterRoom => Message.IsEnterRoom;

    /// <summary>
    /// Gets a value indicating whether 渲染为普通聊天卡片.
    /// </summary>
    public bool ShowNormalRow => IsDanmaku && !IsSuperChat;

    /// <summary>
    /// Gets a value indicating whether 渲染为系统提示行（连接、重连、下播等）.
    /// </summary>
    public bool ShowSystemRow => IsSystem && !IsEnterRoom;

    /// <summary>
    /// Gets 合并后的条数文案（同文案刷屏时显示 ×N）；未合并为 null.
    /// </summary>
    public string? MergeText => _mergeCount > 1 ? $"×{_mergeCount}" : null;

    /// <summary>
    /// 把同文案的新消息并入当前条目。
    /// </summary>
    public void IncrementMerge()
    {
        _mergeCount++;
        Raise(nameof(MergeText));
    }

    /// <summary>
    /// Gets 进房提示文案.
    /// </summary>
    public string? EnterText => Message.IsEnterRoom ? $"{Message.UserName} 进入直播间" : null;

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
    /// Gets 头衔图标；没有头衔或未取到头衔表时为空.
    /// </summary>
    public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? TitleIconSource { get; }

    /// <summary>
    /// Gets 是否显示头衔图标.
    /// </summary>
    public bool HasTitleIcon => TitleIconSource is not null;

    /// <summary>
    /// Gets 头像位图；按 64px 解码，避免每条消息按原图解码把内存打爆.
    /// </summary>
    public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? AvatarSource
    {
        get
        {
            if (AvatarUri is not { } uri)
            {
                return null;
            }

            return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri)
            {
                DecodePixelWidth = 64,
                DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
            };
        }
    }

    /// <summary>
    /// Gets 勋章文案（名称 + 等级），避免两个 TextBlock 之间出现空白.
    /// </summary>
    public string? MedalText
        => string.IsNullOrEmpty(Message.MedalName)
            ? null
            : string.IsNullOrEmpty(Message.MedalLevel)
                ? Message.MedalName
                : $"{Message.MedalName} {Message.MedalLevel}";

    /// <summary>
    /// Gets 昵称画刷（装扮颜色）；没有时回落到默认.
    /// </summary>
    public Brush? NameBrush => _nameBrush;

    /// <summary>
    /// Gets 勋章画刷.
    /// </summary>
    public Brush? MedalBrush => _medalBrush;

    /// <summary>
    /// Gets 勋章底色：用勋章色做低透明度混入，保持材质感.
    /// </summary>
    public Brush? MedalBackgroundBrush => _medalGradientBrush ?? _medalBackgroundBrush;

    /// <summary>
    /// Gets 勋章文字画刷；优先使用 v2 文字色.
    /// </summary>
    public Brush? MedalTextBrush => _medalTextBrush;

    /// <summary>
    /// Gets 舰长船锚图标；非舰长或无数据时为空.
    /// </summary>
    public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? MedalGuardIconSource { get; }

    /// <summary>
    /// Gets 是否显示船锚图标.
    /// </summary>
    public bool HasMedalGuardIcon => MedalGuardIconSource is not null;

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

    private static Microsoft.UI.Xaml.Media.Imaging.BitmapImage? BuildTitleIcon(
        string? titleId,
        IReadOnlyDictionary<string, Richasy.BiliKernel.Models.Media.LiveTitleItem>? resources)
    {
        if (string.IsNullOrEmpty(titleId) || resources is null || !resources.TryGetValue(titleId, out var title))
        {
            return null;
        }

        return TryCreateAbsoluteUri(title.Url, out var uri)
            ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri)
            : null;
    }

    private static Dictionary<string, Richasy.BiliKernel.Models.Appearance.BiliImage>? BuildEmoteIndex(
        string? text,
        IReadOnlyDictionary<string, Richasy.BiliKernel.Models.Media.LiveDanmakuEmote>? emotes,
        IReadOnlyDictionary<string, Richasy.BiliKernel.Models.Media.LiveDanmakuEmote>? resources)
    {
        var result = new Dictionary<string, Richasy.BiliKernel.Models.Appearance.BiliImage>(StringComparer.Ordinal);
        if (emotes is not null)
        {
            foreach (var pair in emotes)
            {
                // 表情地址可能是协议相对（//i0.hdslb.com/...）或非法值；
                // 直接 new Uri 会在 UI 线程抛异常并让整个聊天列表崩掉。
                if (TryCreateAbsoluteUri(pair.Value.Url, out var uri))
                {
                    result[$"[{pair.Key}]"] = new Richasy.BiliKernel.Models.Appearance.BiliImage(uri);
                }
            }
        }

        // 官方表情只带 official_xxx，正文是 [doge]；用表情包资源按文本补齐。
        if (!string.IsNullOrEmpty(text) && resources is { Count: > 0 })
        {
            foreach (Match match in EmoteTokenPattern().Matches(text))
            {
                var key = $"[{match.Groups[1].Value}]";
                if (result.ContainsKey(key))
                {
                    continue;
                }

                if (resources.TryGetValue(match.Groups[1].Value, out var resource)
                    && TryCreateAbsoluteUri(resource.Url, out var uri))
                {
                    result[key] = new Richasy.BiliKernel.Models.Appearance.BiliImage(uri);
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\[([^\[\]]+)\]")]
    private static partial System.Text.RegularExpressions.Regex EmoteTokenPattern();

    private static bool TryCreateAbsoluteUri(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var normalized = url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
        return Uri.TryCreate(normalized, UriKind.Absolute, out uri!);
    }

    private static Microsoft.UI.Xaml.Media.Imaging.BitmapImage? CreateBitmap(string? url, int decodeWidth)
    {
        if (!TryCreateAbsoluteUri(url, out var uri))
        {
            return null;
        }

        return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri)
        {
            DecodePixelWidth = decodeWidth,
            DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
        };
    }

    private static Brush? CreateGradientBrush(string? start, string? end)
    {
        var hasStart = TryParseColor(start, out var startColor);
        var hasEnd = TryParseColor(end, out var endColor);
        if (!hasStart && !hasEnd)
        {
            return null;
        }

        if (!hasStart)
        {
            startColor = endColor;
        }

        if (!hasEnd)
        {
            endColor = startColor;
        }

        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
        };
        brush.GradientStops.Add(new GradientStop { Color = startColor, Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = endColor, Offset = 1 });
        return brush;
    }

    private static bool TryParseColor(string? value, out Windows.UI.Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = value.TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rgb))
        {
            color = Windows.UI.Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }

        if (hex.Length == 8 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var argb8))
        {
            // 直播勋章色是 #RRGGBBAA。
            color = Windows.UI.Color.FromArgb((byte)argb8, (byte)(argb8 >> 24), (byte)(argb8 >> 16), (byte)(argb8 >> 8));
            return true;
        }

        return false;
    }

    private static Brush? CreateTintBrush(string? color, byte alpha)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        try
        {
            var parsed = AppToolkit.HexToColor(color);
            return new SolidColorBrush(Windows.UI.Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B));
        }
        catch
        {
            return null;
        }
    }

    private static Brush? CreateBrush(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        try
        {
            var parsed = AppToolkit.HexToColor(color);
            // 直播间昵称色/勋章色可能很深；在深色卡片上先按亮度提亮，保证可读。
            var luminance = (0.299 * parsed.R) + (0.587 * parsed.G) + (0.114 * parsed.B);
            if (luminance < 140)
            {
                var factor = (140 - luminance) / 140 * 0.75;
                parsed = Windows.UI.Color.FromArgb(
                    255,
                    (byte)(parsed.R + ((255 - parsed.R) * factor)),
                    (byte)(parsed.G + ((255 - parsed.G) * factor)),
                    (byte)(parsed.B + ((255 - parsed.B) * factor)));
            }

            return new SolidColorBrush(parsed);
        }
        catch
        {
            return null;
        }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
