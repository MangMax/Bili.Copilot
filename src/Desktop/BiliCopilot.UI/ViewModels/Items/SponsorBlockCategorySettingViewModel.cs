// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 空降助手单个类别的行为设置项.
/// </summary>
public sealed partial class SponsorBlockCategorySettingViewModel : ObservableObject
{
    private readonly Action<SponsorBlockCategory, SponsorBlockBehavior> _onBehaviorChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockCategorySettingViewModel"/> class.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <param name="behavior">当前行为配置.</param>
    /// <param name="displayName">类别的本地化名称.</param>
    /// <param name="onBehaviorChanged">行为变化时的回调.</param>
    public SponsorBlockCategorySettingViewModel(
        SponsorBlockCategory category,
        SponsorBlockBehavior behavior,
        string displayName,
        Action<SponsorBlockCategory, SponsorBlockBehavior> onBehaviorChanged)
    {
        Category = category;
        _behavior = behavior;
        DisplayName = displayName;
        _onBehaviorChanged = onBehaviorChanged;
    }

    /// <summary>
    /// 片段类别.
    /// </summary>
    public SponsorBlockCategory Category { get; }

    /// <summary>
    /// 类别的本地化名称.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 可选的行为列表.
    /// </summary>
    public IReadOnlyList<SponsorBlockBehavior> AvailableBehaviors { get; }
        = Enum.GetValues<SponsorBlockBehavior>();

    /// <summary>
    /// 当前行为配置.
    /// </summary>
    [ObservableProperty]
    private SponsorBlockBehavior _behavior;

    partial void OnBehaviorChanged(SponsorBlockBehavior value)
        => _onBehaviorChanged(Category, value);
}
