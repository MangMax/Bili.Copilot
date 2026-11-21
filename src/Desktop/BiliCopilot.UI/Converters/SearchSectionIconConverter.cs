// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Icons;
using BiliCopilot.UI.Models.Constants;
using Microsoft.UI.Xaml.Data;

namespace BiliCopilot.UI.Converters;

internal sealed partial class SearchSectionIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var sectionType = (SearchSectionType)value;
        return sectionType switch
        {
            SearchSectionType.Video => IconGlyph.VideoClip,
            SearchSectionType.Anime => IconGlyph.Dust,
            SearchSectionType.Live => IconGlyph.Live,
            SearchSectionType.User => IconGlyph.People,
            SearchSectionType.Cinema => IconGlyph.MoviesAndTv,
            SearchSectionType.Article => IconGlyph.DocumentBulletList,
            _ => throw new ArgumentOutOfRangeException(nameof(sectionType), sectionType, null)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
