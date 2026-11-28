// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace BiliCopilot.UI.Converters;

internal sealed partial class ColorTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is Color color ? Format(color) : default;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();

    public static string Format(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
