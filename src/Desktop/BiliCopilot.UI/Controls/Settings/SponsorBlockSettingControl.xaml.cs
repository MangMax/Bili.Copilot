// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
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

    /// <inheritdoc/>
    protected override void OnControlLoaded() => ViewModel.ThemeChanged += OnThemeChanged;

    /// <inheritdoc/>
    protected override void OnControlUnloaded() => ViewModel.ThemeChanged -= OnThemeChanged;

    /// <summary>
    /// 主题切换后让未自定义的类别重新解析主题色板颜色.
    /// <para>
    /// <c>EffectiveColor</c> 是计算属性，主题字典换了以后不会自己通知，
    /// 因此需要在这里显式刷新一次.
    /// </para>
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (var item in ViewModel.SponsorBlockCategorySettings)
        {
            item.RefreshThemeColor();
        }
    }

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
            // 支持调节透明度：自定义颜色可以带 alpha，进度条标记会按所选透明度呈现.
            IsAlphaEnabled = true,
            IsAlphaSliderVisible = true,
            IsAlphaTextInputVisible = true,
            IsColorChannelTextInputVisible = true,
            IsHexInputVisible = true,
            Color = SponsorBlockColorHelper.ToColor(item.EffectiveColor),
        };

        // 浮层宽度交给内容自行撑开：ColorPicker 模板自带 MinWidth 312，
        // 之前写死宽度会让浮层比取色面板更窄，右侧的透明度与颜色通道输入被裁掉.
        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(picker);
        panel.Children.Add(CreateColorPreview(item));

        var resetButton = new Button
        {
            Content = ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockColorReset),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(resetButton, item.ColorResetAutomationId);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            resetButton,
            ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockColorReset));

        panel.Children.Add(resetButton);

        var flyout = new Flyout
        {
            Content = panel,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
        };

        picker.ColorChanged += (_, args) => item.SetCustomColor(SponsorBlockColorHelper.ToArgb(args.NewColor));
        resetButton.Click += (_, _) =>
        {
            item.ResetColor();
            flyout.Hide();
        };

        flyout.ShowAt(button);
    }

    /// <summary>
    /// 构建浮层底部的实时预览：色块 + 颜色值文本，让调整透明度的结果当场可见.
    /// </summary>
    private FrameworkElement CreateColorPreview(SponsorBlockCategorySettingViewModel item)
    {
        var swatch = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            Background = SponsorBlockColorHelper.CreateBrush(item.EffectiveColor),
        };

        var text = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Text = item.ColorDisplayText,
        };

        var preview = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { swatch, text },
        };

        // 浮层里的预览跟随选择器实时变化，而不是等关闭浮层后才在设置页看到结果.
        item.PropertyChanged += OnPreviewSourceChanged;
        preview.Unloaded += (_, _) => item.PropertyChanged -= OnPreviewSourceChanged;

        return preview;

        void OnPreviewSourceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not (nameof(SponsorBlockCategorySettingViewModel.EffectiveColor)
                or nameof(SponsorBlockCategorySettingViewModel.ColorDisplayText)))
            {
                return;
            }

            swatch.Background = SponsorBlockColorHelper.CreateBrush(item.EffectiveColor);
            text.Text = item.ColorDisplayText;
        }
    }
}
