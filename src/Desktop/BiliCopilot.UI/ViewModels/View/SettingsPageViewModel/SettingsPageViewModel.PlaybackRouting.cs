// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.PlaybackRouting;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BiliCopilot.UI.ViewModels.View;

public sealed partial class SettingsPageViewModel
{
    [ObservableProperty]
    public partial int PlaybackRouteIndex { get; set; } = (int)PlaybackRouteSettings.ReadPreference();

    partial void OnPlaybackRouteIndexChanged(int value)
    {
        // ComboBox can briefly have no selection while its items are initialized.
        if (Enum.IsDefined((PlaybackRoutePreference)value))
        {
            SettingsToolkit.WriteLocalSetting(SettingNames.PlaybackRoutePreference, value);
        }
    }
}
