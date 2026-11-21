// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Icons;
using BiliCopilot.UI.Models.Constants;
using Microsoft.UI.Xaml.Data;

namespace BiliCopilot.UI.Converters;

internal sealed partial class PinIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is PinContentType type
            ? type switch
            {
                PinContentType.Pgc => IconGlyph.Dust,
                PinContentType.User => IconGlyph.Person,
                PinContentType.Video => IconGlyph.VideoClip,
                PinContentType.Live => IconGlyph.VideoChat,
                _ => IconGlyph.DocumentBulletList,
            }
            : (object)IconGlyph.Question;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
