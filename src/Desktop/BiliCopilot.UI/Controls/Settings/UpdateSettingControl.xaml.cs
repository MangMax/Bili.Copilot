// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;

namespace BiliCopilot.UI.Controls.Settings;

/// <summary>
/// 应用更新设置控件.
/// </summary>
public sealed partial class UpdateSettingControl : SettingsPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSettingControl"/> class.
    /// </summary>
    public UpdateSettingControl() => InitializeComponent();

    private AppUpdateViewModel Update { get; } = GlobalDependencies.Kernel.GetRequiredService<AppUpdateViewModel>();
}
