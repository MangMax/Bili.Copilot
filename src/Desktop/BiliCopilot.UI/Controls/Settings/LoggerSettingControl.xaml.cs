// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using Richasy.MpvKernel;
using Windows.Storage;
using Windows.System;

namespace BiliCopilot.UI.Controls.Settings;

/// <summary>
/// 日志设置控件.
/// </summary>
public sealed partial class LoggerSettingControl : SettingsPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerSettingControl"/> class.
    /// </summary>
    public LoggerSettingControl() => InitializeComponent();

    private static string LoggerFolder => Path.Combine(Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.Path, "Logger");

    private async void OnOpenLoggerFolderButtonClick(object sender, RoutedEventArgs e)
    {
        var folder = await StorageFolder.GetFolderFromPathAsync(LoggerFolder).AsTask();
        _ = await Launcher.LaunchFolderAsync(folder).AsTask();
    }

    protected override void OnControlLoaded()
    {
        AppLevelComboBox.SelectedIndex = ViewModel.AppLogLevel switch
        {
            AppLogLevel.Warning => 1,
            AppLogLevel.Information => 2,
            AppLogLevel.Debug => 3,
            _ => 0
        };

        var index = ViewModel.MpvLogLevel switch
        {
            MpvLogLevel.V => 3,
            MpvLogLevel.Info => 2,
            MpvLogLevel.Warn => 1,
            MpvLogLevel.Error => 0,
            _ => 0
        };
        MpvLevelComboBox.SelectedIndex = index;
    }

    private void OnAppLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = AppLevelComboBox.SelectedIndex;
        if (index >= 0 && IsLoaded)
        {
            ViewModel.AppLogLevel = index switch
            {
                3 => AppLogLevel.Debug,
                2 => AppLogLevel.Information,
                1 => AppLogLevel.Warning,
                _ => AppLogLevel.Error
            };
        }
    }

    private void OnMpvLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = MpvLevelComboBox.SelectedIndex;
        if (index >= 0 && IsLoaded)
        {
            ViewModel.MpvLogLevel = index switch
            {
                3 => MpvLogLevel.V,
                2 => MpvLogLevel.Info,
                1 => MpvLogLevel.Warn,
                _ => MpvLogLevel.Error
            };
        }
    }
}
