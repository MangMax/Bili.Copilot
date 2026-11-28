// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using System.Xml.Linq;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 设置页二级导航策略、本地化键与既有设置控件覆盖契约测试.
/// </summary>
public sealed class SettingsNavigationPolicyTests
{
    private static readonly string[] ExpectedResourceKeys =
    [
        "SettingsGroupExperience",
        "SettingsGroupContentRecommend",
        "SettingsGroupSystemServices",
        "SettingsCategoryPlayback",
        "SettingsCategoryPlaybackDescription",
        "SettingsCategoryAppearanceNavigation",
        "SettingsCategoryAppearanceNavigationDescription",
        "SettingsCategoryStorageDownload",
        "SettingsCategoryStorageDownloadDescription",
        "SettingsCategorySystemNotification",
        "SettingsCategorySystemNotificationDescription",
        "SettingsCategoryUpdate",
        "SettingsCategoryUpdateDescription",
        "SettingsCategoryAIService",
        "SettingsCategoryAIServiceDescription",
        "SettingsSubsectionPlaybackNetworkQuality",
        "SettingsSubsectionPlaybackContinuous",
        "SettingsSubsectionPlayerWindow",
        "SettingsSubsectionAppearance",
        "SettingsSubsectionNavigationInteraction",
        "SettingsSubsectionDownloadScreenshot",
        "SettingsSubsectionCache",
        "SettingsSubsectionNotificationWindow",
        "SettingsSubsectionDiagnostics",
        "SettingsSubsectionUpdatePreferences",
        "UpdateAutoDownloadDescription",
        "UpdateDownloadSourceDescription",
    ];

    private static readonly string[] ExpectedSettingsControls =
    [
        "PlayerControlSettingControl",
        "PlayerBehaviorSettingControl",
        "SubtitleContinuousSettingControl",
        "PlayerWindowSettingControl",
        "MpvExtraSettingControl",
        "SponsorBlockSettingControl",
        "ThemeSettingControl",
        "LayoutSettingSection",
        "NavigationSettingControl",
        "ScrollSettingControl",
        "DownloadSettingControl",
        "ScreenshotSettingControl",
        "CacheSettingControl",
        "NotificationSettingControl",
        "CloseBehaviorSettingControl",
        "LoggerSettingControl",
        "RecommendSourceSettingControl",
        "IncrementalRecommendRefreshSettingControl",
        "SearchRecommendSettingSection",
        "AboutSettingControl",
        "UpdateSettingControl",
        "AIResponseSettingControl",
        "PromptTemplateSettingControl",
    ];

    [Theory]
    [InlineData(null, SettingsSection.Root)]
    [InlineData("", SettingsSection.Root)]
    [InlineData("settings-update", SettingsSection.Update)]
    [InlineData("Update", SettingsSection.Update)]
    [InlineData("Playback", SettingsSection.Playback)]
    [InlineData("AppearanceAndNavigation", SettingsSection.AppearanceAndNavigation)]
    [InlineData("StorageAndDownload", SettingsSection.StorageAndDownload)]
    [InlineData("SystemAndNotification", SettingsSection.SystemAndNotification)]
    [InlineData("AIService", SettingsSection.AIService)]
    [InlineData("unknown-route", SettingsSection.Root)]
    public void ResolveSection_MapsRoutesAndFallsBackToRoot(string? input, SettingsSection expected)
    {
        Assert.Equal(expected, SettingsNavigationPolicy.ResolveSection(input));
    }

    [Fact]
    public void ResolveSection_AcceptsEnumAndRejectsUndefinedEnumValue()
    {
        Assert.Equal(SettingsSection.Playback, SettingsNavigationPolicy.ResolveSection(SettingsSection.Playback));
        Assert.Equal(SettingsSection.Update, SettingsNavigationPolicy.ResolveSection(SettingsSection.Update));
        Assert.Equal(SettingsSection.AIService, SettingsNavigationPolicy.ResolveSection(SettingsSection.AIService));
        Assert.Equal(SettingsSection.Root, SettingsNavigationPolicy.ResolveSection((SettingsSection)999));
    }

    [Fact]
    public void TryGoBack_ReturnsToRootFromEverySubpageAndStopsAtRoot()
    {
        Assert.False(SettingsNavigationPolicy.TryGoBack(SettingsSection.Root, out var rootTarget));
        Assert.Equal(SettingsSection.Root, rootTarget);
        Assert.False(SettingsNavigationPolicy.IsSubpage(SettingsSection.Root));

        foreach (var section in Enum.GetValues<SettingsSection>().Where(s => s != SettingsSection.Root))
        {
            Assert.True(SettingsNavigationPolicy.IsSubpage(section));
            Assert.True(SettingsNavigationPolicy.TryGoBack(section, out var target));
            Assert.Equal(SettingsSection.Root, target);
        }
    }

    [Fact]
    public void GetTransitionIndices_ProducesForwardAndBackwardDirections()
    {
        var (enterPrev, enterCurr) = SettingsNavigationPolicy.GetTransitionIndices(SettingsSection.Root, SettingsSection.Playback);
        Assert.Equal(PageTransitionDirection.FromRight, PageTransitionPolicy.GetStepDirection(PageTransitionKind.Tab, enterPrev, enterCurr));

        var (backPrev, backCurr) = SettingsNavigationPolicy.GetTransitionIndices(SettingsSection.Playback, SettingsSection.Root);
        Assert.Equal(PageTransitionDirection.FromLeft, PageTransitionPolicy.GetStepDirection(PageTransitionKind.Tab, backPrev, backCurr));

        Assert.Equal((-1, -1), SettingsNavigationPolicy.GetTransitionIndices(SettingsSection.Playback, SettingsSection.Playback));
    }

    [Fact]
    public void SectionLifecycleHooks_MatchAIAndStorageSections()
    {
        Assert.True(SettingsNavigationPolicy.ShouldInitializeAIServicesOnEnter(SettingsSection.AIService));
        Assert.False(SettingsNavigationPolicy.ShouldInitializeAIServicesOnEnter(SettingsSection.Playback));

        Assert.True(SettingsNavigationPolicy.ShouldSaveAIServicesOnLeave(SettingsSection.AIService, SettingsSection.Root));
        Assert.False(SettingsNavigationPolicy.ShouldSaveAIServicesOnLeave(SettingsSection.AIService, SettingsSection.AIService));
        Assert.False(SettingsNavigationPolicy.ShouldSaveAIServicesOnLeave(SettingsSection.Playback, SettingsSection.Root));

        Assert.True(SettingsNavigationPolicy.ShouldRefreshCacheSizeOnEnter(SettingsSection.StorageAndDownload));
        Assert.False(SettingsNavigationPolicy.ShouldRefreshCacheSizeOnEnter(SettingsSection.Root));
    }

    [Fact]
    public void Resources_ContainAllSettingsReorganizationKeys()
    {
        var resources = ResourcesFileHelper.LoadResources();

        foreach (var key in ExpectedResourceKeys)
        {
            var entries = resources
                .Where(data => (string?)data.Attribute("name") == key)
                .ToList();

            var entry = Assert.Single(entries);
            Assert.False(
                string.IsNullOrWhiteSpace(entry.Element("value")?.Value),
                $"资源键 {key} 的文案为空.");
        }
    }

    [Fact]
    public void SettingsPageXaml_PreservesAllExistingSettingControlsAndDynamicAIPanel()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "Desktop", "BiliCopilot.UI", "Pages", "SettingsPage.xaml");
        var document = XDocument.Load(xamlPath);
        XNamespace settingsNs = "using:BiliCopilot.UI.Controls.Settings";
        XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        foreach (var controlName in ExpectedSettingsControls)
        {
            var matches = document.Descendants(settingsNs + controlName).ToList();
            Assert.Single(matches);
        }

        var aiPanel = document
            .Descendants()
            .Where(e => (string?)e.Attribute(xNs + "Name") == "AIPanel")
            .ToList();
        Assert.Single(aiPanel);

        var updateContainer = document
            .Descendants()
            .Where(e => (string?)e.Attribute(xNs + "Name") == "UpdateContainer")
            .ToList();
        Assert.Single(updateContainer);
    }

    [Fact]
    public void UpdateControls_SurfaceVersionAndNotesOutsideUpdateSettingControlWithoutFlyout()
    {
        var repoRoot = FindRepoRoot();
        var aboutPath = Path.Combine(repoRoot, "src", "Desktop", "BiliCopilot.UI", "Controls", "Settings", "AboutSettingControl.xaml");
        var updatePath = Path.Combine(repoRoot, "src", "Desktop", "BiliCopilot.UI", "Controls", "Settings", "UpdateSettingControl.xaml");
        var aboutDoc = XDocument.Load(aboutPath);
        var updateDoc = XDocument.Load(updatePath);
        XNamespace xamlNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.Empty(aboutDoc.Descendants(xamlNs + "Flyout"));
        Assert.Contains(
            aboutDoc.Descendants(),
            e => (string?)e.Attribute("AutomationProperties.AutomationId") == "CheckForUpdates");
        Assert.Contains(
            aboutDoc.Descendants(),
            e => (string?)e.Attribute("AutomationProperties.AutomationId") == "AboutAppVersion"
                 && (string?)e.Attribute("IsClickEnabled") != "True");
        Assert.Contains(
            aboutDoc.Descendants(),
            e => (string?)e.Attribute("AutomationProperties.AutomationId") == "AboutUpdateNotes"
                 && (string?)e.Attribute("IsExpanded") == "True");
        Assert.Contains(
            aboutDoc.Descendants(),
            e => (string?)e.Attribute("AutomationProperties.AutomationId") == "AboutUpdateReleaseNotes");
        Assert.DoesNotContain(updateDoc.Descendants(), e => (string?)e.Attribute("AutomationProperties.AutomationId") == "CheckForUpdates");
        Assert.DoesNotContain(updateDoc.Descendants(), e => (string?)e.Attribute("Header") == "{ext:Locale Name=AppName}");
        Assert.DoesNotContain(updateDoc.Descendants(), e => (string?)e.Attribute("AutomationProperties.AutomationId") == "SettingsUpdateReleaseNotes");
        Assert.DoesNotContain(updateDoc.Descendants(), e => (string?)e.Attribute("AutomationProperties.AutomationId") == "SettingsUpdateDownloadProgress");

        string[] expectedUpdateAutomationIds =
        [
            "UpdateCheckInterval",
            "UpdateCheckInterval",
            "UpdateAutoDownload",
            "UpdateDownloadSource",
            "UpdateMirrorPrefixes",
        ];

        var actualIds = updateDoc
            .Descendants()
            .Select(e => (string?)e.Attribute("AutomationProperties.AutomationId"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var id in expectedUpdateAutomationIds)
        {
            Assert.Contains(id, actualIds);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "Desktop", "BiliCopilot.UI", "Pages", "SettingsPage.xaml")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("无法定位仓库根目录.");
    }
}
