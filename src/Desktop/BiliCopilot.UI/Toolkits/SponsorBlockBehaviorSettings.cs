// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 空降助手按类别配置的行为设置.
/// </summary>
internal static class SponsorBlockBehaviorSettings
{
    private static readonly SponsorBlockCategory[] _categories =
    [
        SponsorBlockCategory.Sponsor,
        SponsorBlockCategory.SelfPromo,
        SponsorBlockCategory.ExclusiveAccess,
        SponsorBlockCategory.Interaction,
        SponsorBlockCategory.PoiHighlight,
        SponsorBlockCategory.Intro,
        SponsorBlockCategory.Outro,
        SponsorBlockCategory.Preview,
        SponsorBlockCategory.Padding,
        SponsorBlockCategory.Filler,
        SponsorBlockCategory.MusicOfftopic,
        SponsorBlockCategory.Unknown,
    ];

    /// <summary>
    /// 设置页展示的类别顺序.
    /// </summary>
    public static IReadOnlyList<SponsorBlockCategory> Categories => _categories;

    /// <summary>
    /// 读取指定类别的行为配置，未配置时返回默认值.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>该类别当前的行为配置.</returns>
    public static SponsorBlockBehavior Read(SponsorBlockCategory category)
    {
        var defaultValue = SponsorBlockBehaviorDefaults.GetDefault(category);
        try
        {
            var value = SettingsToolkit.ReadLocalSetting(GetSettingName(category), defaultValue);
            return Enum.IsDefined(value) ? value : defaultValue;
        }
        catch (Exception)
        {
            // 本地设置损坏或版本不兼容时回退到默认行为.
            return defaultValue;
        }
    }

    /// <summary>
    /// 写入指定类别的行为配置.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <param name="behavior">行为配置.</param>
    public static void Write(SponsorBlockCategory category, SponsorBlockBehavior behavior)
        => SettingsToolkit.WriteLocalSetting(GetSettingName(category), behavior);

    private static SettingNames GetSettingName(SponsorBlockCategory category)
        => category switch
        {
            SponsorBlockCategory.Sponsor => SettingNames.SponsorBlockBehaviorSponsor,
            SponsorBlockCategory.SelfPromo => SettingNames.SponsorBlockBehaviorSelfPromo,
            SponsorBlockCategory.ExclusiveAccess => SettingNames.SponsorBlockBehaviorExclusiveAccess,
            SponsorBlockCategory.Interaction => SettingNames.SponsorBlockBehaviorInteraction,
            SponsorBlockCategory.PoiHighlight => SettingNames.SponsorBlockBehaviorPoiHighlight,
            SponsorBlockCategory.Intro => SettingNames.SponsorBlockBehaviorIntro,
            SponsorBlockCategory.Outro => SettingNames.SponsorBlockBehaviorOutro,
            SponsorBlockCategory.Preview => SettingNames.SponsorBlockBehaviorPreview,
            SponsorBlockCategory.Padding => SettingNames.SponsorBlockBehaviorPadding,
            SponsorBlockCategory.Filler => SettingNames.SponsorBlockBehaviorFiller,
            SponsorBlockCategory.MusicOfftopic => SettingNames.SponsorBlockBehaviorMusicOfftopic,
            _ => SettingNames.SponsorBlockBehaviorUnknown,
        };
}
