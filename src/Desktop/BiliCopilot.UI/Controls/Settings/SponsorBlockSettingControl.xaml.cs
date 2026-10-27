// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

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

    /// <summary>
    /// 打开该类别专属的颜色选择浮层.
    /// <para>
    /// 浮层内容在代码里构建：每一行都需要自己的 <see cref="ColorPicker"/> 实例，
    /// 且初始颜色要按类别解析（自定义颜色或当前主题画刷），用共享模板反而更难保证状态独立.
    /// </para>
    /// </summary>
    private void OnColorButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SponsorBlockCategorySettingViewModel item } button)
        {
            return;
        }

        // 初始颜色在订阅事件之前赋值，避免打开浮层时把主题色误存成自定义颜色.
        var picker = new ColorPicker
        {
            IsAlphaEnabled = false,
            IsAlphaSliderVisible = false,
            IsAlphaTextInputVisible = false,
            IsColorChannelTextInputVisible = true,
            IsHexInputVisible = true,
            Color = ResolvePickerColor(item),
        };

        var resetButton = new Button
        {
            Content = ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockColorReset),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(resetButton, item.ColorResetAutomationId);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            resetButton,
            ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockColorReset));

        var panel = new StackPanel
        {
            Width = 280,
            Spacing = 8,
            Children = { picker, resetButton },
        };

        var flyout = new Flyout
        {
            Content = panel,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
        };

        picker.ColorChanged += (_, args) => item.SetCustomColor(ToOpaqueArgb(args.NewColor));
        resetButton.Click += (_, _) =>
        {
            item.ResetColor();
            flyout.Hide();
        };

        flyout.ShowAt(button);
    }

    /// <summary>
    /// 解析颜色选择器的初始颜色：优先自定义颜色，否则取该类别的主题画刷.
    /// </summary>
    private static Windows.UI.Color ResolvePickerColor(SponsorBlockCategorySettingViewModel item)
    {
        if (item.CustomColor is uint argb)
        {
            return SponsorBlockColorHelper.ToColor(argb);
        }

        if (Application.Current?.Resources.TryGetValue(SponsorBlockCategoryPalette.GetBrushKey(item.Category), out var value) == true
            && value is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return SponsorBlockColorHelper.ToColor(SponsorBlockCategoryPalette.GetFallbackColor(item.Category));
    }

    /// <summary>
    /// 标记统一以全局不透明度呈现，颜色本身强制不透明，避免选择器带回的半透明值再叠一层.
    /// </summary>
    private static uint ToOpaqueArgb(Windows.UI.Color color)
        => 0xFF000000u
        | ((uint)color.R << 16)
        | ((uint)color.G << 8)
        | color.B;
}
