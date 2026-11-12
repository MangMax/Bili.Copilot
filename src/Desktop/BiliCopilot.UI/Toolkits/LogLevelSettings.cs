// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Logging;
using BiliCopilot.UI.Models.Constants;
using Richasy.WinUIKernel.Share.Toolkits;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 日志级别设置的启动读取与运行时应用.
/// </summary>
internal static class LogLevelSettings
{
    /// <summary>
    /// 执行一次性迁移并读取应用日志级别.
    /// </summary>
    /// <returns>应用日志级别.</returns>
    public static AppLogLevel ReadAppLogLevel()
    {
        ISettingsToolkit settings = new SettingsToolkit();
        LogLevelMigration.Run(new SettingsStore(settings));
        return settings.ReadLocalSetting(SettingNames.AppLogLevel.ToString(), LogLevelMigration.DefaultAppLevel);
    }

    /// <summary>
    /// 保存并立即应用应用日志级别.
    /// </summary>
    /// <param name="level">新的应用日志级别.</param>
    public static void ApplyAppLogLevel(AppLogLevel level)
    {
        SettingsToolkit.WriteLocalSetting(SettingNames.AppLogLevel, level);
        AppLogLevelSwitch.Default.Apply(level);
    }

    private sealed class SettingsStore : ILogLevelSettingsStore
    {
        private readonly ISettingsToolkit _settings;

        public SettingsStore(ISettingsToolkit settings) => _settings = settings;

        public bool IsSettingKeyExist(string key) => _settings.IsSettingKeyExist(key);

        public T Read<T>(string key, T defaultValue) => _settings.ReadLocalSetting(key, defaultValue);

        public void Write<T>(string key, T value) => _settings.WriteLocalSetting(key, value);
    }
}
