// Copyright (c) Bili Copilot. All rights reserved.

using NavigationSymbol = FluentIcons.Common.Symbol;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace BiliCopilot.UI.Icons;

public static partial class IconGlyph
{
    public static FontFamily FontFamily { get; } = new("ms-appx:///Assets/BiliIcons.ttf#Bili Copilot Icons");

    public static string SelectChatEmpty(bool filled) => filled ? ChatEmptyFilled : ChatEmpty;

    public static string SelectHeart(bool filled) => filled ? HeartFilled : Heart;

    public static string SelectReward(bool filled) => filled ? RewardFilled : Reward;

    public static string SelectSparkle(bool filled) => filled ? SparkleFilled : Sparkle;

    public static string SelectStar(bool? filled) => filled == true ? StarFilled : Star;

    public static string SelectThumbLike(bool? filled) => filled == true ? ThumbLikeFilled : ThumbLike;

    public static string ToFilled(string glyph)
        => glyph switch
        {
            TextBulletListSquareSparkle => TextBulletListSquareSparkleFilled,
            ChatSparkle => ChatSparkleFilled,
            DocumentOnePageSparkle => DocumentOnePageSparkleFilled,
            Question => QuestionFilled,
            _ => glyph,
        };

    internal static string FromNavigationSymbol(NavigationSymbol symbol, bool filled = false)
        => symbol switch
        {
            NavigationSymbol.Rocket => filled ? RocketFilled : Rocket,
            NavigationSymbol.DesignIdeas => filled ? DesignIdeasFilled : DesignIdeas,
            NavigationSymbol.VideoClip => filled ? VideoClipFilled : VideoClip,
            NavigationSymbol.VideoChat => filled ? VideoChatFilled : VideoChat,
            NavigationSymbol.Dust => filled ? DustFilled : Dust,
            NavigationSymbol.FilmstripPlay => filled ? FilmstripPlayFilled : FilmstripPlay,
            NavigationSymbol.DocumentBulletList => filled ? DocumentBulletListFilled : DocumentBulletList,
            NavigationSymbol.Chat => filled ? ChatFilled : Chat,
            NavigationSymbol.Settings => filled ? SettingsFilled : Settings,
            NavigationSymbol.StarLineHorizontal3 => filled ? StarLineHorizontal3Filled : StarLineHorizontal3,
            NavigationSymbol.CalendarClock => filled ? CalendarClockFilled : CalendarClock,
            NavigationSymbol.History => filled ? HistoryFilled : History,
            _ => string.Empty,
        };
}

internal sealed partial class NavigationIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is NavigationSymbol symbol ? IconGlyph.FromNavigationSymbol(symbol, parameter is "Filled") : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
