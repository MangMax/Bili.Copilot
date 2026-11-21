// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Icons;
using Microsoft.UI.Xaml.Data;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Converters;

internal sealed partial class NotifyMessageIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var type = (NotifyMessageType)value;
        return type switch
        {
            NotifyMessageType.Reply => IconGlyph.Comment,
            NotifyMessageType.At => IconGlyph.CallConnecting,
            NotifyMessageType.Like => IconGlyph.ThumbLike,
            _ => throw new Exception("Unknown type."),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
