// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 标题栏上的更新入口.
/// </summary>
public sealed partial class UpdateButton : UpdateButtonBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateButton"/> class.
    /// </summary>
    public UpdateButton() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
        => _ = ViewModel.AutoCheckAsync();

    private void OnButtonClick(object sender, RoutedEventArgs e)
        => ViewModel.PrimaryActionCommand.Execute(default);
}

/// <summary>
/// 更新入口控件的基类.
/// </summary>
public abstract class UpdateButtonBase : LayoutUserControlBase<AppUpdateViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateButtonBase"/> class.
    /// </summary>
    protected UpdateButtonBase() => ViewModel = this.Get<AppUpdateViewModel>();
}
