// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.PlaybackRouting;

// The settings adapter persists only a stable preference value, never a signed URL.
internal static class PlaybackRouteSettings
{
    public static PlaybackRoutePreference ReadPreference()
    {
        var value = SettingsToolkit.ReadLocalSetting(SettingNames.PlaybackRoutePreference, 0);
        return Enum.IsDefined((PlaybackRoutePreference)value) ? (PlaybackRoutePreference)value : PlaybackRoutePreference.Auto;
    }
}
