// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 动态卡片操作面板.
/// </summary>
public sealed partial class MomentItemOperationPanel : MomentItemOperationPanelBase
{
    public static readonly DependencyProperty SeparatorPaddingProperty = DependencyProperty.Register(
        nameof(SeparatorPadding), typeof(Thickness), typeof(MomentItemOperationPanel), new PropertyMetadata(new Thickness(0, 12, 0, 0)));

    /// <summary>Space between the divider and its actions.</summary>
    public Thickness SeparatorPadding
    {
        get => (Thickness)GetValue(SeparatorPaddingProperty);
        set => SetValue(SeparatorPaddingProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MomentItemOperationPanel"/> class.
    /// </summary>
    public MomentItemOperationPanel()
    {
        InitializeComponent();
    }
}

/// <summary>
/// 动态卡片操作面板基类.
/// </summary>
public abstract class MomentItemOperationPanelBase : LayoutUserControlBase<MomentItemViewModel>
{
}
