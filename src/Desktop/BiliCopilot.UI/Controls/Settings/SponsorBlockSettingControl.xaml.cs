// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;

namespace BiliCopilot.UI.Controls.Settings;

/// <summary>
/// 空降助手设置控件.
/// </summary>
public sealed partial class SponsorBlockSettingControl : SettingsPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockSettingControl"/> class.
    /// </summary>
    public SponsorBlockSettingControl() => InitializeComponent();

    /// <summary>
    /// 下拉框创建完成后，把选中项对齐到该类别已保存的行为.
    /// <para>
    /// 选中项由代码显式赋值，而不是用 TwoWay 绑定：
    /// 绑定会在数据项换绑的瞬间把旧选中项回写到新的数据项上，正是串值的来源.
    /// </para>
    /// </summary>
    private void OnBehaviorComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox { Tag: SponsorBlockCategorySettingViewModel item } combo)
        {
            item.BeginBindingRefresh();
            try
            {
                combo.SelectedItem = item.Behavior;
            }
            finally
            {
                item.EndBindingRefresh();
            }
        }
    }

    /// <summary>
    /// 下拉框的选中项变化时更新对应类别.
    /// <para>
    /// 选中项为空（下拉框正在绑定）时由 <see cref="SponsorBlockCategorySettingViewModel.ApplySelection"/> 忽略；
    /// 只有用户真正换选另一个行为时，才由设置项判断是否写入本地设置.
    /// </para>
    /// </summary>
    private void OnBehaviorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { Tag: SponsorBlockCategorySettingViewModel item } combo)
        {
            item.ApplySelection(combo.SelectedItem);
        }
    }
}
