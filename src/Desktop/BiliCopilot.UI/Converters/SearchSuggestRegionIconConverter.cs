// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Icons;
using Microsoft.UI.Xaml.Data;

namespace BiliCopilot.UI.Converters;

internal sealed partial class SearchSuggestRegionIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var id = value as string;
        return id switch
        {
            "space" => IconGlyph.Person,
            "history" => IconGlyph.History,
            _ => IconGlyph.Search,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
