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
    /// (资源键, 主题字典键) 到已解析画刷的缓存，<c>null</c> 表示该主题字典里没有这个键.
    /// <para>
    /// 只在 UI 线程访问：调用方是设置页控件与进度条，颜色变化事件也统一派发到 UI 队列.
    /// </para>
    /// </summary>
    private static readonly Dictionary<(string Key, string ThemeKey), Brush?> _themeBrushCache = [];

    /// <summary>
    /// 合并字典的遍历深度上限.
    /// </summary>
    private const int MaxThemeDictionaryDepth = 16;

    private static readonly string[] LightThemeKeys = ["Light"];
    private static readonly string[] DarkThemeKeys = ["Dark", "Default"];
    private static readonly string[] HighContrastThemeKeys = ["HighContrast"];

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
    /// <param name="theme">调用方所在元素的实际主题，用于选取浅色 / 深色变体.</param>
    /// <returns>该类别当前生效的标记画刷.</returns>
    public static Brush ResolveCategoryBrush(SponsorBlockCategory category, ElementTheme theme)
    {
        if (IsHighContrastInEffect)
        {
            // 高对比度下由系统高亮色接管，自定义颜色不生效，
            // 与 Styles/Overrides.xaml 里 HighContrast 字典的资源覆盖保持一致.
            return ResolveThemeBrush(category, theme);
        }

        return SponsorBlockColorSettings.Read(category) is uint argb
            ? CreateBrush(argb)
            : ResolveThemeBrush(category, theme);
    }

    /// <summary>
    /// 解析某个类别在主题色板下的颜色值（忽略自定义颜色）.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <param name="theme">调用方所在元素的实际主题，用于选取浅色 / 深色变体.</param>
    /// <returns>该类别主题色板对应的 0xAARRGGBB 颜色值.</returns>
    public static uint ResolveThemeColor(SponsorBlockCategory category, ElementTheme theme)
        => ResolveThemeBrush(category, theme) is SolidColorBrush brush
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
        => !IsHighContrastInEffect && SponsorBlockColorSettings.Read(category) is not null;

    /// <summary>
    /// 按指定主题解析一个主题画刷资源，供代码构建的界面（例如取色浮层）使用.
    /// <para>
    /// 直接查 <see cref="Application.Current"/> 的资源只会得到应用主题对应的变体，
    /// 而本应用的主题是设在窗口内容上的，两者可能不同，因此这里显式按主题字典取值.
    /// </para>
    /// </summary>
    /// <param name="key">资源键.</param>
    /// <param name="theme">调用方所在元素的实际主题.</param>
    /// <returns>该主题下的画刷；资源缺失时返回 <c>null</c>，由调用方决定兜底值.</returns>
    public static Brush? ResolveThemedResource(string key, ElementTheme theme)
        => FindThemedBrush(key, theme);

    /// <summary>
    /// 当前是否处于高对比度模式：此时标记由系统高亮色决定，与是否自定义无关.
    /// </summary>
    private static bool IsHighContrastInEffect
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

    private static Brush ResolveThemeBrush(SponsorBlockCategory category, ElementTheme theme)
        => FindThemedBrush(SponsorBlockCategoryPalette.GetBrushKey(category), theme)
            ?? CreateBrush(SponsorBlockCategoryPalette.GetFallbackColor(category));

    /// <summary>
    /// 在主题字典里查找画刷.
    /// <para>
    /// 应用级资源只暴露它自己那一层的 <c>ThemeDictionaries</c>，
    /// 而类别画刷定义在合并进来的 <c>Styles/Overrides.xaml</c> 里，
    /// 因此需要逐层遍历合并字典才能按指定主题取到正确的变体.
    /// </para>
    /// </summary>
    private static Brush? FindThemedBrush(string key, ElementTheme theme)
    {
        // 主题字典是静态资源，同一个 (键, 主题字典) 的结果不会变，缓存下来.
        // 否则每次切换主题要为 12 个类别各递归遍历一遍合并字典，平白拉长卡顿时间.
        foreach (var themeKey in GetThemeDictionaryKeys(theme))
        {
            if (_themeBrushCache.TryGetValue((key, themeKey), out var cached))
            {
                if (cached is not null)
                {
                    return cached;
                }

                continue;
            }

            var found = FindInThemeDictionaries(Application.Current?.Resources, key, themeKey);
            _themeBrushCache[(key, themeKey)] = found;
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static Brush? FindInThemeDictionaries(
        ResourceDictionary? dictionary,
        string key,
        string themeKey,
        int depth = 0)
    {
        // 合并字典理论上不会成环，但遍历是递归的，加个深度上限兜底，
        // 万一哪天资源互相引用也不会把 UI 线程栈打爆.
        if (dictionary is null || depth > MaxThemeDictionaryDepth)
        {
            return null;
        }

        if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var themed)
            && themed is ResourceDictionary themedDictionary
            && themedDictionary.TryGetValue(key, out var value)
            && value is Brush brush)
        {
            return brush;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (FindInThemeDictionaries(merged, key, themeKey, depth + 1) is Brush nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// 主题对应的字典键，按优先级排列.
    /// <para>
    /// 应用自己的字典用 <c>Light</c> / <c>Dark</c>，
    /// 而框架资源（XamlControlsResources）习惯用 <c>Default</c> 表示深色，故深色时依次尝试.
    /// </para>
    /// </summary>
    private static string[] GetThemeDictionaryKeys(ElementTheme theme)
    {
        if (IsHighContrastInEffect)
        {
            return HighContrastThemeKeys;
        }

        return theme == ElementTheme.Light ? LightThemeKeys : DarkThemeKeys;
    }

    /// <summary>
    /// 把 <see cref="Color"/> 还原为 0xAARRGGBB.
    /// </summary>
    /// <param name="color">待转换的颜色.</param>
    /// <returns>对应的 0xAARRGGBB 颜色值.</returns>
    public static uint ToArgb(Color color)
        => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
}
