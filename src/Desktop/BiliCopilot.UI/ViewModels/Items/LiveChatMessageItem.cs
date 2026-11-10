// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>UI-owned images and brushes for a live chat row.</summary>
public sealed partial class LiveChatMessageItem : LiveChatMessagePresentation
{
    private readonly LiveConnectorViewModel _owner;
    private Brush? _nameBrush;
    private readonly Brush? _medalBrush;
    private readonly Brush? _medalBackgroundBrush;
    private readonly Brush? _medalGradientBrush;
    private readonly Brush? _medalTextBrush;

    /// <summary>Creates a presentation row on the UI thread.</summary>
    public LiveChatMessageItem(LiveConnectorViewModel owner, LiveChatMessage message, Func<LiveChatSystemKind, string>? systemText,
        DateTimeOffset now, Func<LiveChatBadgeKind, string>? badgeText = null, Func<int, string>? priceFormat = null,
        IReadOnlyDictionary<string, LiveDanmakuEmote>? emoteResources = null,
        IReadOnlyDictionary<string, LiveTitleItem>? titleResources = null)
        : base(message, systemText, now, badgeText, priceFormat, emoteResources)
    {
        _owner = owner;
        _nameBrush = CreateBrush(message.NameColor);
        _medalBrush = CreateBrush(message.MedalColor);
        _medalBackgroundBrush = CreateTintBrush(message.MedalColor, 0x2E);
        _medalGradientBrush = CreateGradientBrush(message.MedalColorStart, message.MedalColorEnd) ?? _medalBackgroundBrush;
        _medalTextBrush = CreateBrush(message.MedalTextColor) ?? _medalBrush;
        MedalGuardIconSource = CreateBitmap(message.MedalGuardIcon, 32);
        if (message.GuardLevel > 0 && MedalGuardIconSource is not null)
        {
            BadgeText = null;
        }

        TitleIconSource = BuildTitleIcon(message.UserTitleId, titleResources);
        AvatarSource = TryCreateAbsoluteUri(message.Avatar, out var avatar) ? avatar : null;
    }

    /// <summary>Gets whether this message has a usable user identity.</summary>
    public bool HasUser => Message.UserId > 0;

    [RelayCommand(CanExecute = nameof(HasUser))]
    private void OpenUser() => _owner.OpenChatUser(Message);

    [RelayCommand(CanExecute = nameof(HasUser))]
    private void BlockUser() => _owner.BlockChatUser(Message.UserId);

    internal void BlockText(string text) => _owner.BlockChatText(text);

    internal void CopyText(string text) => _owner.CopyChatText(text);

    /// <summary>Gets the title icon.</summary>
    public BitmapImage? TitleIconSource { get; private set; }

    /// <summary>Gets whether a title icon is available.</summary>
    public bool HasTitleIcon => TitleIconSource is not null;

    /// <summary>Gets the avatar URI for the shared image loader.</summary>
    public Uri? AvatarSource { get; }

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
    public BitmapImage? MedalGuardIconSource { get; }

    /// <summary>
    /// Gets 是否显示船锚图标.
    /// </summary>
    public bool HasMedalGuardIcon => MedalGuardIconSource is not null;

    /// <summary>Updates titles loaded after this row was created.</summary>
    public void UpdateTitles(IReadOnlyDictionary<string, LiveTitleItem>? resources)
    {
        if (TitleIconSource is not null)
        {
            return;
        }

        TitleIconSource = BuildTitleIcon(Message.UserTitleId, resources);
        Raise(nameof(TitleIconSource));
        Raise(nameof(HasTitleIcon));
    }

    /// <summary>Uses the theme foreground when a supplied nickname color would be unreadable.</summary>
    public void UpdateTheme(ElementTheme theme)
    {
        _nameBrush = null;
        if (TryParseColor(Message.NameColor, out var color))
        {
            var brightness = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
            if (theme == ElementTheme.Dark ? brightness >= 140 : brightness <= 140)
            {
                _nameBrush = new SolidColorBrush(color);
            }
        }

        Raise(nameof(NameBrush));
    }

    private static BitmapImage? BuildTitleIcon(
        string? titleId,
        IReadOnlyDictionary<string, LiveTitleItem>? resources)
    {
        if (string.IsNullOrEmpty(titleId) || resources is null || !resources.TryGetValue(titleId, out var title))
        {
            return null;
        }

        return CreateBitmap(title.Url, 48);
    }

    private static BitmapImage? CreateBitmap(string? url, int decodeWidth)
    {
        if (!TryCreateAbsoluteUri(url, out var uri))
        {
            return null;
        }

        return new BitmapImage(uri)
        {
            DecodePixelWidth = decodeWidth,
            DecodePixelType = DecodePixelType.Logical,
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
        => TryParseColor(color, out var parsed)
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B))
            : null;

    private static Brush? CreateBrush(string? color)
        => TryParseColor(color, out var parsed) ? new SolidColorBrush(parsed) : null;
}
