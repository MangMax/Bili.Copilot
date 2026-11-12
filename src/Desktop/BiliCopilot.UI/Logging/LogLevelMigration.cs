// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.Logging;

/// <summary>
/// 日志级别拆分的一次性迁移.
/// </summary>
public static class LogLevelMigration
{
    /// <summary>
    /// 应用日志级别的保守默认值.
    /// </summary>
    public const AppLogLevel DefaultAppLevel = AppLogLevel.Information;

    /// <summary>
    /// 补齐应用日志级别并写入迁移标记. 不读取或改写 mpv 级别.
    /// </summary>
    /// <param name="store">设置存取.</param>
    public static void Run(ILogLevelSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (store.IsSettingKeyExist(SettingNames.LogLevelSplitMigrationCompleted.ToString()))
        {
            return;
        }

        if (!store.IsSettingKeyExist(SettingNames.AppLogLevel.ToString()))
        {
            store.Write(SettingNames.AppLogLevel.ToString(), DefaultAppLevel);
        }

        store.Write(SettingNames.LogLevelSplitMigrationCompleted.ToString(), true);
    }
}
