// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 设置页面视图模型的空降助手部分.
/// </summary>
public sealed partial class SettingsPageViewModel
{
    /// <summary>
    /// 空降助手按类别配置的行为.
    /// </summary>
    public ObservableCollection<SponsorBlockCategorySettingViewModel> SponsorBlockCategorySettings { get; } = [];

    private void InitializeSponsorBlockSettings()
    {
        if (SponsorBlockCategorySettings.Count > 0)
        {
            return;
        }

        foreach (var category in SponsorBlockBehaviorSettings.Categories)
        {
            SponsorBlockCategorySettings.Add(new SponsorBlockCategorySettingViewModel(
                category,
                SponsorBlockBehaviorSettings.Read(category),
                SponsorBlockCategoryHelper.GetDisplayName(category),
                SponsorBlockBehaviorSettings.Write,
                SponsorBlockColorSettings.Read(category),
                SponsorBlockColorSettings.Write,
                SponsorBlockColorSettings.Reset,

                // 默认色取决于元素所在主题，纯逻辑层无法判断，
                // 这里只给一个兜底值；控件加载后会用实际主题重新推入.
                null,
                ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockColorDefault)));
        }
    }
}
