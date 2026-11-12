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
        SelectByTag(AppLevelComboBox, ViewModel.AppLogLevel.ToString());
        SelectByTag(MpvLevelComboBox, ViewModel.MpvLogLevel.ToString());
    }

    private void OnAppLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded
            && AppLevelComboBox.SelectedItem is ComboBoxItem { Tag: string tag }
            && Enum.TryParse<AppLogLevel>(tag, out var level))
        {
            ViewModel.AppLogLevel = level;
        }
    }

    private void OnMpvLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded
            && MpvLevelComboBox.SelectedItem is ComboBoxItem { Tag: string tag }
            && Enum.TryParse<MpvLogLevel>(tag, out var level))
        {
            ViewModel.MpvLogLevel = level;
        }
    }

    private static void SelectByTag(ComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items)
        {
            if (item is ComboBoxItem { Tag: string itemTag }
                && string.Equals(itemTag, tag, StringComparison.Ordinal))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }
}
