// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 空降助手颜色值到 UI 颜色类型的转换.
/// </summary>
/// <remarks>
/// 纯逻辑层只处理 0xAARRGGBB，只有这里和视图代码会接触 <see cref="Color"/> / <see cref="Brush"/>.
/// </remarks>
internal static class SponsorBlockColorHelper
{
    private static readonly AccessibilitySettings _accessibilitySettings = new();

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

    /// <summary>
    /// 解析某个类别最终生效的标记颜色.
    /// <para>
    /// 设置页色块与进度条标记共用同一处解析，避免两边各自实现而出现口径不一致；
    /// 未配置自定义颜色时回退到该类别在 <c>Styles/Overrides.xaml</c> 里的主题画刷.
    /// </para>
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>该类别当前生效的标记画刷.</returns>
    public static Brush ResolveCategoryBrush(SponsorBlockCategory category)
    {
        if (IsThemePaletteInEffect)
        {
            // 高对比度下由系统高亮色接管，自定义颜色不生效，
            // 与 Styles/Overrides.xaml 里 HighContrast 字典的资源覆盖保持一致.
            return ResolveThemeBrush(category);
        }

        return SponsorBlockColorSettings.Read(category) is uint argb
            ? CreateBrush(argb)
            : ResolveThemeBrush(category);
    }

    /// <summary>
    /// 解析某个类别在主题色板下的颜色值（忽略自定义颜色）.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>该类别主题色板对应的 0xAARRGGBB 颜色值.</returns>
    public static uint ResolveThemeColor(SponsorBlockCategory category)
        => ResolveThemeBrush(category) is SolidColorBrush brush
            ? ToArgb(brush.Color)
            : SponsorBlockCategoryPalette.GetFallbackColor(category);

    /// <summary>
    /// 判断某个类别当前是否真的由自定义颜色决定显示.
    /// <para>
    /// 高对比度下自定义颜色不生效，此时应返回 <c>false</c>;
    /// 进度条据此决定是否再叠一层默认半透明——自定义颜色的透明度由用户自己决定，不再额外衰减.
    /// </para>
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>自定义颜色生效时为 <c>true</c>.</returns>
    public static bool IsCustomColorInEffect(SponsorBlockCategory category)
        => !IsThemePaletteInEffect && SponsorBlockColorSettings.Read(category) is not null;

    /// <summary>
    /// 当前是否由主题色板决定标记颜色：高对比度下系统高亮色接管，与是否自定义无关.
    /// </summary>
    private static bool IsThemePaletteInEffect
    {
        get
        {
            try
            {
                return _accessibilitySettings.HighContrast;
            }
            catch (Exception)
            {
                // 无 UI 线程或系统不支持时按普通模式处理.
                return false;
            }
        }
    }

    private static Brush ResolveThemeBrush(SponsorBlockCategory category)
        => ResolveResource(
            SponsorBlockCategoryPalette.GetBrushKey(category),
            CreateBrush(SponsorBlockCategoryPalette.GetFallbackColor(category)));

    /// <summary>
    /// 把 <see cref="Color"/> 还原为 0xAARRGGBB.
    /// </summary>
    /// <param name="color">待转换的颜色.</param>
    /// <returns>对应的 0xAARRGGBB 颜色值.</returns>
    public static uint ToArgb(Color color)
        => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    private static Brush ResolveResource(string key, Brush fallback)
        => Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Brush brush
            ? brush
            : fallback;
}
