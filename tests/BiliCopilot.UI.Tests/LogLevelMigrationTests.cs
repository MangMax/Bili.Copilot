// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Logging;
using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.Tests;

public class LogLevelMigrationTests
{
    [Fact]
    public void Run_WhenAppLevelMissing_SeedsConservativeDefaultAndWritesMarker()
    {
        var store = new InMemoryLogLevelSettingsStore();

        LogLevelMigration.Run(store);

        Assert.Equal(
            AppLogLevel.Information,
            store.Read(SettingNames.AppLogLevel.ToString(), AppLogLevel.Error));
        Assert.True(store.Read(SettingNames.LogLevelSplitMigrationCompleted.ToString(), false));
        Assert.DoesNotContain(SettingNames.MpvLogLevel.ToString(), store.Writes);
    }

    [Fact]
    public void Run_WhenLegacyMpvLevelExists_PreservesItAndSeedsDefaultAppLevel()
    {
        var store = new InMemoryLogLevelSettingsStore();
        store.Write(SettingNames.MpvLogLevel.ToString(), "V");
        store.Writes.Clear();

        LogLevelMigration.Run(store);

        Assert.Equal(
            "V",
            store.Read(SettingNames.MpvLogLevel.ToString(), "Warn"));
        Assert.Equal(
            AppLogLevel.Information,
            store.Read(SettingNames.AppLogLevel.ToString(), AppLogLevel.Error));
        Assert.DoesNotContain(SettingNames.MpvLogLevel.ToString(), store.Writes);
    }

    [Fact]
    public void Run_WhenAppLevelAlreadyExists_DoesNotOverwriteIt()
    {
        var store = new InMemoryLogLevelSettingsStore();
        store.Write(SettingNames.AppLogLevel.ToString(), AppLogLevel.Warning);

        LogLevelMigration.Run(store);

        Assert.Equal(
            AppLogLevel.Warning,
            store.Read(SettingNames.AppLogLevel.ToString(), AppLogLevel.Error));
        Assert.True(store.Read(SettingNames.LogLevelSplitMigrationCompleted.ToString(), false));
    }

    [Fact]
    public void Run_WhenMarkerExists_DoesNotWriteAnything()
    {
        var store = new InMemoryLogLevelSettingsStore();
        store.Write(SettingNames.LogLevelSplitMigrationCompleted.ToString(), true);
        store.Writes.Clear();

        LogLevelMigration.Run(store);

        Assert.Empty(store.Writes);
    }

    [Fact]
    public void Run_AfterUserChangesAppLevel_DoesNotRestoreOldValueOnNextStartup()
    {
        var store = new InMemoryLogLevelSettingsStore();
        LogLevelMigration.Run(store);
        store.Write(SettingNames.AppLogLevel.ToString(), AppLogLevel.Debug);
        store.Writes.Clear();

        LogLevelMigration.Run(store);

        Assert.Equal(
            AppLogLevel.Debug,
            store.Read(SettingNames.AppLogLevel.ToString(), AppLogLevel.Error));
        Assert.Empty(store.Writes);
    }

    [Fact]
    public void Run_AfterUserChangesAppLevel_DoesNotModifyMpvLevel()
    {
        var store = new InMemoryLogLevelSettingsStore();
        store.Write(SettingNames.MpvLogLevel.ToString(), "Info");
        LogLevelMigration.Run(store);
        store.Write(SettingNames.AppLogLevel.ToString(), AppLogLevel.Debug);
        store.Writes.Clear();

        LogLevelMigration.Run(store);

        Assert.Equal(
            "Info",
            store.Read(SettingNames.MpvLogLevel.ToString(), "Warn"));
        Assert.DoesNotContain(SettingNames.MpvLogLevel.ToString(), store.Writes);
    }

    private sealed class InMemoryLogLevelSettingsStore : ILogLevelSettingsStore
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        public List<string> Writes { get; } = [];

        public bool IsSettingKeyExist(string key) => _values.ContainsKey(key);

        public T Read<T>(string key, T defaultValue)
            => _values.TryGetValue(key, out var value) && value is T typed
                ? typed
                : defaultValue;

        public void Write<T>(string key, T value)
        {
            _values[key] = value!;
            Writes.Add(key);
        }
    }
}
