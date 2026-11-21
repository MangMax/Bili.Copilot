// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class AppUpdateViewModel
{
    public bool HasAvailableUpdate => !string.IsNullOrEmpty(AvailableVersion);

    internal void TryShowUpdateNotification()
    {
        if (!Version.TryParse(AvailableVersion, out var available)
            || !SettingsToolkit.ReadLocalSetting(SettingNames.IsNotificationEnabled, true)
            || !SettingsToolkit.ReadLocalSetting(SettingNames.IsAppUpdateNotificationEnabled, true))
        {
            return;
        }

        var previous = SettingsToolkit.ReadLocalSetting(SettingNames.LastNotifiedAppUpdateVersion, string.Empty);
        if (Version.TryParse(previous, out var notified) && notified >= available)
        {
            return;
        }

        try
        {
            var manager = AppNotificationManager.Default;
            if (manager.Setting != AppNotificationSetting.Enabled)
            {
                return;
            }

            var notification = new AppNotificationBuilder()
                .SetGroup("app-update")
                .SetTag("available")
                .AddArgument("page", SettingsNavigationPolicy.UpdateNotificationRoute)
                .AddText(ResourceToolkit.GetLocalizedString(StringNames.AppUpdateNotification))
                .AddText(string.Format(ResourceToolkit.GetLocalizedString(StringNames.UpdateAvailableFormat), AvailableVersion))
                .BuildNotification();
            manager.Show(notification);
            if (notification.Id != 0)
            {
                SettingsToolkit.WriteLocalSetting(SettingNames.LastNotifiedAppUpdateVersion, AvailableVersion);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发送应用更新通知失败。");
        }
    }
}
