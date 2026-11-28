// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Player;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 播放窗口初始布局/播放模式偏好的解析契约：
/// 记忆上次按记忆值，固定值忽略记忆值，异常记忆值回落到普通窗口.
/// </summary>
public sealed class PlayerWindowPreferenceResolverTests
{
    [Theory]
    [InlineData(PlayerPresenterMode.FullScreen, PlayerPresenterMode.FullScreen)]
    [InlineData(PlayerPresenterMode.CompactOverlay, PlayerPresenterMode.CompactOverlay)]
    [InlineData(PlayerPresenterMode.Default, PlayerPresenterMode.Default)]
    public void ResolveStartupPresenter_RememberFollowsLastRemembered(
        PlayerPresenterMode lastRemembered, PlayerPresenterMode expected)
        => Assert.Equal(
            expected,
            PlayerWindowPreferenceResolver.ResolveStartupPresenter(PlayerPresenterModePreference.Remember, lastRemembered));

    [Theory]
    [InlineData(PlayerPresenterMode.FullScreen)]
    [InlineData(PlayerPresenterMode.CompactOverlay)]
    [InlineData(PlayerPresenterMode.Default)]
    public void ResolveStartupPresenter_AlwaysNormalIgnoresMemory(PlayerPresenterMode lastRemembered)
        => Assert.Equal(
            PlayerPresenterMode.Default,
            PlayerWindowPreferenceResolver.ResolveStartupPresenter(PlayerPresenterModePreference.Normal, lastRemembered));

    [Theory]
    [InlineData(PlayerPresenterMode.FullScreen)]
    [InlineData(PlayerPresenterMode.CompactOverlay)]
    [InlineData(PlayerPresenterMode.Default)]
    public void ResolveStartupPresenter_AlwaysFullScreenIgnoresMemory(PlayerPresenterMode lastRemembered)
        => Assert.Equal(
            PlayerPresenterMode.FullScreen,
            PlayerWindowPreferenceResolver.ResolveStartupPresenter(PlayerPresenterModePreference.FullScreen, lastRemembered));

    [Theory]
    [InlineData(PlayerPresenterMode.FullScreen)]
    [InlineData(PlayerPresenterMode.CompactOverlay)]
    [InlineData(PlayerPresenterMode.Default)]
    public void ResolveStartupPresenter_AlwaysCompactOverlayIgnoresMemory(PlayerPresenterMode lastRemembered)
        => Assert.Equal(
            PlayerPresenterMode.CompactOverlay,
            PlayerWindowPreferenceResolver.ResolveStartupPresenter(PlayerPresenterModePreference.CompactOverlay, lastRemembered));

    [Fact]
    public void ResolveStartupPresenter_RememberFallsBackToNormalForUnknownMemory()
        => Assert.Equal(
            PlayerPresenterMode.Default,
            PlayerWindowPreferenceResolver.ResolveStartupPresenter(PlayerPresenterModePreference.Remember, (PlayerPresenterMode)99));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ResolveInitialDefaultLayout_RememberFollowsMemory(bool lastUsedDefaultLayout, bool expected)
        => Assert.Equal(
            expected,
            PlayerWindowPreferenceResolver.ResolveInitialDefaultLayout(PlayerLayoutPreference.Remember, lastUsedDefaultLayout));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveInitialDefaultLayout_AlwaysFillIgnoresMemory(bool lastUsedDefaultLayout)
        => Assert.False(
            PlayerWindowPreferenceResolver.ResolveInitialDefaultLayout(PlayerLayoutPreference.Fill, lastUsedDefaultLayout));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveInitialDefaultLayout_AlwaysDefaultIgnoresMemory(bool lastUsedDefaultLayout)
        => Assert.True(
            PlayerWindowPreferenceResolver.ResolveInitialDefaultLayout(PlayerLayoutPreference.Default, lastUsedDefaultLayout));

    [Fact]
    public void SettingName_PersistedKeysAreStable()
    {
        Assert.Equal("LastPlayerUseDefaultLayout", SettingNames.LastPlayerUseDefaultLayout.ToString());
        Assert.Equal("PlayerLayoutPreference", SettingNames.PlayerLayoutPreference.ToString());
        Assert.Equal("PlayerPresenterModePreference", SettingNames.PlayerPresenterModePreference.ToString());
        Assert.Equal("LastPlayerPresenterMode", SettingNames.LastPlayerPresenterMode.ToString());
    }

    [Fact]
    public void Resources_ContainNonEmptySettingText()
    {
        string[] keys =
        [
            "SettingsSubsectionPlaybackNetworkQuality",
            "SettingsSubsectionPlaybackContinuous",
            "SettingsSubsectionPlayerWindow",
            "PlayerWindowMode",
            "PlayerWindowModeDescription",
            "PlayerWindowModeSingle",
            "PlayerWindowModeIndependent",
            "PlayerLayoutPreference",
            "PlayerLayoutPreferenceDescription",
            "PlayerLayoutRemember",
            "PlayerLayoutFill",
            "PlayerLayoutDefault",
            "StartupPresenterMode",
            "StartupPresenterModeDescription",
            "PresenterRemember",
            "PresenterFullScreen",
            "PresenterCompactOverlay",
            "PresenterNormal",
            "PlaybackNetworkQualityDescription",
            "PlaybackContinuousDescription",
            "PlaybackWindowDescription",
        ];

        var resources = ResourcesFileHelper.LoadResources();
        foreach (var key in keys)
        {
            var entry = Assert.Single(resources, data => (string?)data.Attribute("name") == key);
            Assert.False(
                string.IsNullOrWhiteSpace(entry.Element("value")?.Value),
                $"资源键 {key} 的文案为空.");
        }
    }
}
