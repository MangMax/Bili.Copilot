// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 空降助手类别文案.
/// </summary>
internal static class SponsorBlockCategoryHelper
{
    /// <summary>
    /// 获取类别的本地化显示名称.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>本地化后的类别名称.</returns>
    public static string GetDisplayName(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategorySponsor),
            SponsorBlockCategory.SelfPromo => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategorySelfPromo),
            SponsorBlockCategory.ExclusiveAccess => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryExclusiveAccess),
            SponsorBlockCategory.Interaction => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryInteraction),
            SponsorBlockCategory.PoiHighlight => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryPoiHighlight),
            SponsorBlockCategory.Intro => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryIntro),
            SponsorBlockCategory.Outro => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryOutro),
            SponsorBlockCategory.Preview => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryPreview),
            SponsorBlockCategory.Padding => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryPadding),
            SponsorBlockCategory.Filler => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryFiller),
            SponsorBlockCategory.MusicOfftopic => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryMusicOfftopic),
            _ => ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockCategoryUnknown),
        };
}
