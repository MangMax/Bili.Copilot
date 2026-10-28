// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降片段类别与进度条标记颜色资源的映射.
/// </summary>
/// <remarks>
/// 颜色资源位于 <c>Styles/Overrides.xaml</c>，并按浅色 / 深色主题提供不同变体；
/// <see cref="GetFallbackColor"/> 只是资源缺失时的兜底，取值与深色主题保持一致.
/// 类别元数据统一来自 <see cref="SponsorBlockCategoryCatalog"/>.
/// </remarks>
public static class SponsorBlockCategoryPalette
{
    /// <summary>
    /// 获取类别对应的画刷资源键.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns><c>Styles/Overrides.xaml</c> 中的画刷资源键.</returns>
    public static string GetBrushKey(SponsorBlockCategory category)
        => SponsorBlockCategoryCatalog.Get(category).BrushResourceKey;

    /// <summary>
    /// 获取类别对应的兜底颜色（ARGB）.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>0xAARRGGBB 格式的颜色值.</returns>
    public static uint GetFallbackColor(SponsorBlockCategory category)
        => SponsorBlockCategoryCatalog.Get(category).FallbackColor;
}
