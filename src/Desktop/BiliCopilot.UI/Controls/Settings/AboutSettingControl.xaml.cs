// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;

namespace BiliCopilot.UI.Controls.Settings;

/// <summary>
/// 关于设置控件.
/// </summary>
public sealed partial class AboutSettingControl : SettingsPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutSettingControl"/> class.
    /// </summary>
    public AboutSettingControl() => InitializeComponent();

    private AppUpdateViewModel Update { get; } = GlobalDependencies.Kernel.GetRequiredService<AppUpdateViewModel>();
}
