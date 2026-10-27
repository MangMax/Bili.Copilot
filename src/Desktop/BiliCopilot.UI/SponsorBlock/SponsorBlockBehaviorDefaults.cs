// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手各类片段的默认行为.
/// </summary>
public static class SponsorBlockBehaviorDefaults
{
    /// <summary>
    /// 获取指定类别的默认行为.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>该类别未配置时使用的行为.</returns>
    public static SponsorBlockBehavior GetDefault(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.SelfPromo => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.ExclusiveAccess => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.Intro => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.Outro => SponsorBlockBehavior.Skip,
            SponsorBlockCategory.Interaction => SponsorBlockBehavior.ShowButton,
            SponsorBlockCategory.PoiHighlight => SponsorBlockBehavior.ShowButton,
            _ => SponsorBlockBehavior.None,
        };

    /// <summary>
    /// 创建包含所有类别默认行为的映射.
    /// </summary>
    /// <returns>类别到默认行为的只读映射.</returns>
    public static IReadOnlyDictionary<SponsorBlockCategory, SponsorBlockBehavior> CreateMap()
    {
        var map = new Dictionary<SponsorBlockCategory, SponsorBlockBehavior>();
        foreach (var category in Enum.GetValues<SponsorBlockCategory>())
        {
            map[category] = GetDefault(category);
        }

        return map;
    }
}
