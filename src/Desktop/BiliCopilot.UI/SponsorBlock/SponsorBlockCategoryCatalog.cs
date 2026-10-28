// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手单个类别在界面、设置与绘制上的全部元数据.
/// </summary>
/// <remarks>
/// <see cref="SettingName"/> / <see cref="ColorSettingName"/> / <see cref="DisplayNameKey"/>
/// 保存的是设置项与本地化资源的名称，而不是枚举，以便该表与 WinUI 无关、可以直接进行单元测试.
/// </remarks>
/// <param name="Category">片段类别.</param>
/// <param name="ApiName">接口返回的类别名称，未知类别为空.</param>
/// <param name="DefaultBehavior">未配置时的默认行为.</param>
/// <param name="SettingName">行为设置项名称，对应 <c>SettingNames</c> 的成员名.</param>
/// <param name="ColorSettingName">自定义颜色设置项名称，对应 <c>SettingNames</c> 的成员名.</param>
/// <param name="BrushResourceKey">进度条标记画刷的资源键.</param>
/// <param name="FallbackColor">资源缺失时的兜底颜色（0xAARRGGBB）.</param>
/// <param name="DisplayNameKey">本地化显示名称的资源键，对应 <c>StringNames</c> 的成员名.</param>
public sealed record SponsorBlockCategoryDescriptor(
    SponsorBlockCategory Category,
    string ApiName,
    SponsorBlockBehavior DefaultBehavior,
    string SettingName,
    string ColorSettingName,
    string BrushResourceKey,
    uint FallbackColor,
    string DisplayNameKey);

/// <summary>
/// 空降助手类别的唯一元数据表.
/// <para>
/// 新增类别时只需要在 <see cref="All"/> 中追加一项，并补充对应的设置项与本地化资源，
/// 行为默认值、进度条颜色、自定义颜色设置名称和显示文案都会自动生效.
/// </para>
/// </summary>
public static class SponsorBlockCategoryCatalog
{
    private static readonly SponsorBlockCategoryDescriptor[] _all =
    [
        new(
            SponsorBlockCategory.Sponsor,
            "sponsor",
            SponsorBlockBehavior.Skip,
            "SponsorBlockBehaviorSponsor",
            "SponsorBlockColorSponsor",
            "SponsorBlockMarkerSponsorBrush",
            0xFFD88CA8,
            "SponsorBlockCategorySponsor"),
        new(
            SponsorBlockCategory.SelfPromo,
            "selfpromo",
            SponsorBlockBehavior.Skip,
            "SponsorBlockBehaviorSelfPromo",
            "SponsorBlockColorSelfPromo",
            "SponsorBlockMarkerSelfPromoBrush",
            0xFFD9A06D,
            "SponsorBlockCategorySelfPromo"),
        new(
            SponsorBlockCategory.ExclusiveAccess,
            "exclusive_access",
            SponsorBlockBehavior.Skip,
            "SponsorBlockBehaviorExclusiveAccess",
            "SponsorBlockColorExclusiveAccess",
            "SponsorBlockMarkerExclusiveAccessBrush",
            0xFF73B39E,
            "SponsorBlockCategoryExclusiveAccess"),
        new(
            SponsorBlockCategory.Interaction,
            "interaction",
            SponsorBlockBehavior.ShowButton,
            "SponsorBlockBehaviorInteraction",
            "SponsorBlockColorInteraction",
            "SponsorBlockMarkerInteractionBrush",
            0xFFA996D6,
            "SponsorBlockCategoryInteraction"),
        new(
            SponsorBlockCategory.PoiHighlight,
            "poi_highlight",
            SponsorBlockBehavior.ShowButton,
            "SponsorBlockBehaviorPoiHighlight",
            "SponsorBlockColorPoiHighlight",
            "SponsorBlockMarkerPoiHighlightBrush",
            0xFFD68FB5,
            "SponsorBlockCategoryPoiHighlight"),
        new(
            SponsorBlockCategory.Intro,
            "intro",
            SponsorBlockBehavior.Skip,
            "SponsorBlockBehaviorIntro",
            "SponsorBlockColorIntro",
            "SponsorBlockMarkerIntroBrush",
            0xFF72ADBA,
            "SponsorBlockCategoryIntro"),
        new(
            SponsorBlockCategory.Outro,
            "outro",
            SponsorBlockBehavior.Skip,
            "SponsorBlockBehaviorOutro",
            "SponsorBlockColorOutro",
            "SponsorBlockMarkerOutroBrush",
            0xFF8798CF,
            "SponsorBlockCategoryOutro"),
        new(
            SponsorBlockCategory.Preview,
            "preview",
            SponsorBlockBehavior.None,
            "SponsorBlockBehaviorPreview",
            "SponsorBlockColorPreview",
            "SponsorBlockMarkerPreviewBrush",
            0xFF7DA8C1,
            "SponsorBlockCategoryPreview"),
        new(
            SponsorBlockCategory.Padding,
            "padding",
            SponsorBlockBehavior.None,
            "SponsorBlockBehaviorPadding",
            "SponsorBlockColorPadding",
            "SponsorBlockMarkerPaddingBrush",
            0xFFA4A4A4,
            "SponsorBlockCategoryPadding"),
        new(
            SponsorBlockCategory.Filler,
            "filler",
            SponsorBlockBehavior.None,
            "SponsorBlockBehaviorFiller",
            "SponsorBlockColorFiller",
            "SponsorBlockMarkerFillerBrush",
            0xFF9D8FBE,
            "SponsorBlockCategoryFiller"),
        new(
            SponsorBlockCategory.MusicOfftopic,
            "music_offtopic",
            SponsorBlockBehavior.None,
            "SponsorBlockBehaviorMusicOfftopic",
            "SponsorBlockColorMusicOfftopic",
            "SponsorBlockMarkerMusicOfftopicBrush",
            0xFFCC9D62,
            "SponsorBlockCategoryMusicOfftopic"),
        new(
            SponsorBlockCategory.Unknown,
            string.Empty,
            SponsorBlockBehavior.None,
            "SponsorBlockBehaviorUnknown",
            "SponsorBlockColorUnknown",
            "SponsorBlockMarkerUnknownBrush",
            0xFFBC898B,
            "SponsorBlockCategoryUnknown"),
    ];

    private static readonly Dictionary<SponsorBlockCategory, SponsorBlockCategoryDescriptor> _byCategory =
        _all.ToDictionary(descriptor => descriptor.Category);

    private static readonly SponsorBlockCategoryDescriptor _unknown =
        _byCategory[SponsorBlockCategory.Unknown];

    /// <summary>
    /// 全部类别，顺序即设置页展示顺序.
    /// </summary>
    public static IReadOnlyList<SponsorBlockCategoryDescriptor> All => _all;

    /// <summary>
    /// 获取类别元数据；枚举中出现未登记的类别时回退到 <see cref="SponsorBlockCategory.Unknown"/>.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>该类别的元数据.</returns>
    public static SponsorBlockCategoryDescriptor Get(SponsorBlockCategory category)
        => _byCategory.TryGetValue(category, out var descriptor) ? descriptor : _unknown;

    /// <summary>
    /// 将接口返回的类别字符串转换为类别枚举.
    /// </summary>
    /// <param name="value">接口返回的类别字符串.</param>
    /// <returns>类别枚举，未知类别返回 <see cref="SponsorBlockCategory.Unknown"/>.</returns>
    public static SponsorBlockCategory FromApiName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SponsorBlockCategory.Unknown;
        }

        var normalized = value.Trim().ToLowerInvariant();
        foreach (var descriptor in _all)
        {
            if (descriptor.ApiName.Length > 0
                && string.Equals(descriptor.ApiName, normalized, StringComparison.Ordinal))
            {
                return descriptor.Category;
            }
        }

        return SponsorBlockCategory.Unknown;
    }
}
