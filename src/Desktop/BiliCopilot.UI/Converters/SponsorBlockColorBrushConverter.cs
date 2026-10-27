// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace BiliCopilot.UI.Converters;

/// <summary>
/// 把空降助手的自定义颜色值转换为设置页色块画刷.
/// </summary>
/// <remarks>
/// 值为空表示使用主题色；此时色块由 XAML 里的主题画刷承担，转换器只返回透明画刷.
/// </remarks>
internal sealed partial class SponsorBlockColorBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is uint argb
            ? SponsorBlockColorHelper.CreateBrush(argb)
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
