// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 0xAARRGGBB 颜色值与 <see cref="Windows.UI.Color"/> 之间的转换工具.
/// </summary>
internal static class ArgbColorUtility
{
    /// <summary>
    /// 将 <see cref="Windows.UI.Color"/> 转换为 0xAARRGGBB 颜色值.
    /// </summary>
    /// <param name="color">待转换的颜色.</param>
    /// <returns>对应的 0xAARRGGBB 颜色值.</returns>
    public static uint ToArgb(Windows.UI.Color color)
        => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    /// <summary>
    /// 将 0xAARRGGBB 颜色值转换为 <see cref="Windows.UI.Color"/>.
    /// </summary>
    /// <param name="argb">0xAARRGGBB 格式的颜色值.</param>
    /// <returns>对应的颜色.</returns>
    public static Windows.UI.Color ToColor(uint argb)
        => Windows.UI.Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb);

    /// <summary>
    /// 将 <see cref="Windows.UI.Color"/> 格式化为 RRGGBB 十六进制字符串（不带 # 与透明度）.
    /// </summary>
    /// <param name="color">待格式化的颜色.</param>
    /// <returns>如 FE0302 的 6 位十六进制文本.</returns>
    public static string ToRgbHex(Windows.UI.Color color)
        => (ToArgb(color) & 0xFFFFFF).ToString("X6");
}
