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
    {
        var key = SponsorBlockCategoryCatalog.Get(category).DisplayNameKey;
        return Enum.TryParse<StringNames>(key, out var name)
            ? ResourceToolkit.GetLocalizedString(name)
            : key;
    }
}
