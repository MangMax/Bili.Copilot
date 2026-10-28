// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace BiliCopilot.UI.Converters;

/// <summary>
/// 把空降助手的自定义颜色值转换为设置页色块画刷.
/// </summary>
/// <remarks>
/// 传入的是该类别当前生效的颜色（自定义色或当前主题下的色板色），
/// 因此这里只做 0xAARRGGBB → 画刷的转换，不再判断“是否使用主题色”.
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
