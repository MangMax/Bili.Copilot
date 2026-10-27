// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 空降助手按类别配置的行为设置.
/// </summary>
internal static class SponsorBlockBehaviorSettings
{
    /// <summary>
    /// 设置页展示的类别顺序，与 <see cref="SponsorBlockCategoryCatalog.All"/> 保持一致.
    /// </summary>
    public static IReadOnlyList<SponsorBlockCategory> Categories { get; } =
        SponsorBlockCategoryCatalog.All.Select(descriptor => descriptor.Category).ToArray();

    /// <summary>
    /// 读取指定类别的行为配置，未配置时返回默认值.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>该类别当前的行为配置.</returns>
    public static SponsorBlockBehavior Read(SponsorBlockCategory category)
    {
        var descriptor = SponsorBlockCategoryCatalog.Get(category);
        var defaultValue = descriptor.DefaultBehavior;
        try
        {
            var value = SettingsToolkit.ReadLocalSetting(ToSettingName(descriptor), defaultValue);
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
        => SettingsToolkit.WriteLocalSetting(ToSettingName(SponsorBlockCategoryCatalog.Get(category)), behavior);

    /// <summary>
    /// 元数据表保存的是设置项名称，这里换回强类型枚举.
    /// </summary>
    private static SettingNames ToSettingName(SponsorBlockCategoryDescriptor descriptor)
        => Enum.TryParse<SettingNames>(descriptor.SettingName, out var name)
            ? name
            : SettingNames.SponsorBlockBehaviorUnknown;
}
