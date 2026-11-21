// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Icons;
using BiliCopilot.UI.Models.Constants;
using Microsoft.UI.Xaml.Data;

namespace BiliCopilot.UI.Converters;

internal sealed partial class PgcSectionIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var sectionType = (PgcSectionType)value;
        return sectionType switch
        {
            PgcSectionType.Timeline => IconGlyph.Timeline,
            PgcSectionType.Anime => IconGlyph.Dust,
            PgcSectionType.Movie => IconGlyph.MoviesAndTv,
            PgcSectionType.TV => IconGlyph.Tv,
            PgcSectionType.Documentary => IconGlyph.EarthLeaf,
            _ => throw new ArgumentOutOfRangeException(nameof(sectionType), sectionType, null)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
