// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 标题栏上的更新入口.
/// </summary>
public sealed partial class UpdateButton : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateButton"/> class.
    /// </summary>
    public UpdateButton()
    {
        ViewModel = this.Get<AppUpdateViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// 更新视图模型.
    /// </summary>
    internal AppUpdateViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => _ = ViewModel.AutoCheckAsync();

    private void OnButtonClick(object sender, RoutedEventArgs e)
        => ViewModel.PrimaryActionCommand.Execute(default);
}
