// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 空降助手颜色值到 UI 颜色类型的转换.
/// </summary>
/// <remarks>
/// 纯逻辑层只处理 0xAARRGGBB，只有这里和视图代码会接触 <see cref="Color"/> / <see cref="Brush"/>.
/// </remarks>
internal static class SponsorBlockColorHelper
{
    /// <summary>
    /// 把 0xAARRGGBB 转换为 <see cref="Color"/>.
    /// </summary>
    /// <param name="argb">0xAARRGGBB 格式的颜色值.</param>
    /// <returns>对应的颜色.</returns>
    public static Color ToColor(uint argb)
        => Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb);

    /// <summary>
    /// 把 0xAARRGGBB 转换为纯色画刷.
    /// </summary>
    /// <param name="argb">0xAARRGGBB 格式的颜色值.</param>
    /// <returns>对应的纯色画刷.</returns>
    public static Brush CreateBrush(uint argb) => new SolidColorBrush(ToColor(argb));
}
