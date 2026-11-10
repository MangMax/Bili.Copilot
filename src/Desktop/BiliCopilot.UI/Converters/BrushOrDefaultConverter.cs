// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace BiliCopilot.UI.Converters;

/// <summary>
/// 画刷兜底：值为空时回落到指定主题画刷，避免 Foreground/BorderBrush 为 null 导致文字不可见。
/// </summary>
internal sealed partial class BrushOrDefaultConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Brush brush)
        {
            return brush;
        }

        var key = parameter as string;
        if (string.IsNullOrEmpty(key))
        {
            key = "TextFillColorPrimaryBrush";
        }

        return Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush fallback
            ? fallback
            : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
