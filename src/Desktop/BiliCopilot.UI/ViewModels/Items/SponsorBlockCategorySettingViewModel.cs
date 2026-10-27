// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 空降助手单个类别的行为设置项.
/// </summary>
public sealed class SponsorBlockCategorySettingViewModel : ObservableObject
{
    private readonly Action<SponsorBlockCategory, SponsorBlockBehavior> _onBehaviorChanged;
    private SponsorBlockBehavior _behavior;
    private SponsorBlockBehavior _persistedBehavior;
    private int _bindingRefreshDepth;

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
        _persistedBehavior = behavior;
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
    /// <para>
    /// 只有值真正变化、且与该类别已保存的值不同时才写入本地设置，
    /// 这样下拉框在绑定过程中的重复赋值不会把不该保存的值写进设置.
    /// </para>
    /// </summary>
    public SponsorBlockBehavior Behavior
    {
        get => _behavior;
        set
        {
            if (IsBindingRefreshInProgress || !SetProperty(ref _behavior, value) || value == _persistedBehavior)
            {
                return;
            }

            _persistedBehavior = value;
            _onBehaviorChanged(Category, value);
        }
    }

    private bool IsBindingRefreshInProgress => _bindingRefreshDepth > 0;

    /// <summary>
    /// 接收下拉框的选中项变化，只在选中项是合法行为时生效.
    /// </summary>
    /// <param name="selectedItem">下拉框当前的选中项.</param>
    public void ApplySelection(object? selectedItem)
    {
        if (selectedItem is SponsorBlockBehavior behavior && AvailableBehaviors.Contains(behavior))
        {
            Behavior = behavior;
        }
    }

    /// <summary>
    /// 开始一次绑定刷新。
    /// <para>
    /// 刷新期间控件会把数据项换绑到下拉框上，随之而来的值变化不代表用户操作，
    /// 这段时间内对 <see cref="Behavior"/> 的写入会被忽略.
    /// </para>
    /// </summary>
    public void BeginBindingRefresh() => _bindingRefreshDepth++;

    /// <summary>
    /// 结束一次由 <see cref="BeginBindingRefresh"/> 开启的绑定刷新.
    /// </summary>
    public void EndBindingRefresh()
    {
        if (_bindingRefreshDepth > 0)
        {
            _bindingRefreshDepth--;
        }
    }
}
