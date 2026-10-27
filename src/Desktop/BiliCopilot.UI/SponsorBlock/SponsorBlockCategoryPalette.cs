// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降片段类别与进度条标记颜色资源的映射.
/// </summary>
/// <remarks>
/// 颜色资源位于 <c>Styles/Overrides.xaml</c>，并按浅色 / 深色主题提供不同变体；
/// <see cref="GetFallbackColor"/> 只是资源缺失时的兜底，取值与深色主题保持一致.
/// </remarks>
public static class SponsorBlockCategoryPalette
{
    /// <summary>
    /// 获取类别对应的画刷资源键.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns><c>Styles/Overrides.xaml</c> 中的画刷资源键.</returns>
    public static string GetBrushKey(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => "SponsorBlockMarkerSponsorBrush",
            SponsorBlockCategory.SelfPromo => "SponsorBlockMarkerSelfPromoBrush",
            SponsorBlockCategory.ExclusiveAccess => "SponsorBlockMarkerExclusiveAccessBrush",
            SponsorBlockCategory.Interaction => "SponsorBlockMarkerInteractionBrush",
            SponsorBlockCategory.PoiHighlight => "SponsorBlockMarkerPoiHighlightBrush",
            SponsorBlockCategory.Intro => "SponsorBlockMarkerIntroBrush",
            SponsorBlockCategory.Outro => "SponsorBlockMarkerOutroBrush",
            SponsorBlockCategory.Preview => "SponsorBlockMarkerPreviewBrush",
            SponsorBlockCategory.Padding => "SponsorBlockMarkerPaddingBrush",
            SponsorBlockCategory.Filler => "SponsorBlockMarkerFillerBrush",
            SponsorBlockCategory.MusicOfftopic => "SponsorBlockMarkerMusicOfftopicBrush",
            _ => "SponsorBlockMarkerUnknownBrush",
        };

    /// <summary>
    /// 获取类别对应的兜底颜色（ARGB）.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>0xAARRGGBB 格式的颜色值.</returns>
    public static uint GetFallbackColor(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => 0xFF00D800,
            SponsorBlockCategory.SelfPromo => 0xFFFFD400,
            SponsorBlockCategory.ExclusiveAccess => 0xFF00A878,
            SponsorBlockCategory.Interaction => 0xFFCC00FF,
            SponsorBlockCategory.PoiHighlight => 0xFFFF3B8C,
            SponsorBlockCategory.Intro => 0xFF00E5E5,
            SponsorBlockCategory.Outro => 0xFF4C5BFF,
            SponsorBlockCategory.Preview => 0xFF2AA0E0,
            SponsorBlockCategory.Padding => 0xFF9E9E9E,
            SponsorBlockCategory.Filler => 0xFF8A4DFF,
            SponsorBlockCategory.MusicOfftopic => 0xFFFF9F1A,
            _ => 0xFFE53935,
        };
}
