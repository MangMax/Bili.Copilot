// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手自定义标记颜色的持久化格式转换.
/// </summary>
/// <remarks>
/// 本地设置统一保存为 <c>#AARRGGBB</c> 字符串，既不依赖平台颜色类型，
/// 也便于在配置损坏时直接判定为无效并回退到主题色.
/// </remarks>
public static class SponsorBlockColorParser
{
    /// <summary>
    /// 颜色字符串的长度，包含前导 <c>#</c>.
    /// </summary>
    public const int FormattedLength = 9;

    /// <summary>
    /// 把 ARGB 颜色格式化为 <c>#AARRGGBB</c>.
    /// </summary>
    /// <param name="argb">0xAARRGGBB 格式的颜色值.</param>
    /// <returns>以 <c>#</c> 开头的 8 位十六进制字符串.</returns>
    public static string Format(uint argb)
        => "#" + argb.ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>
    /// 把 ARGB 颜色格式化为设置页展示用的短文本.
    /// <para>
    /// 完全不透明时省略 <c>FF</c> 前缀，只显示 <c>#RRGGBB</c>；
    /// 带透明度时保留 <c>#AARRGGBB</c>，让透明度在文字上也能看出来.
    /// </para>
    /// </summary>
    /// <param name="argb">0xAARRGGBB 格式的颜色值.</param>
    /// <returns>设置页色块旁显示的十六进制文本.</returns>
    public static string ToDisplayString(uint argb)
        => argb >> 24 == 0xFFu
            ? "#" + (argb & 0xFFFFFFu).ToString("X6", CultureInfo.InvariantCulture)
            : Format(argb);

    /// <summary>
    /// 解析 <c>#AARRGGBB</c> 字符串.
    /// </summary>
    /// <param name="value">待解析的字符串，允许首尾空白.</param>
    /// <param name="argb">解析成功时的 0xAARRGGBB 颜色值.</param>
    /// <returns>格式合法时返回 <c>true</c>.</returns>
    public static bool TryParse(string? value, out uint argb)
    {
        argb = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.Length != FormattedLength || text[0] != '#')
        {
            return false;
        }

        return uint.TryParse(
            text.AsSpan(1),
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out argb);
    }
}
