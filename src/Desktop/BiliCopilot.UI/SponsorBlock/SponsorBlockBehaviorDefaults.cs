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
        => SponsorBlockCategoryCatalog.Get(category).DefaultBehavior;
}
