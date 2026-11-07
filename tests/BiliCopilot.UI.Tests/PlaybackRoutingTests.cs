using BiliCopilot.UI.PlaybackRouting;

namespace BiliCopilot.UI.Tests;

public sealed class PlaybackRoutingTests
{
    [Fact]
    public void Auto_PrefersLowerRiskAndPreservesSignedUrl()
    {
        const string backup = "https://upos-sz-mirrorali.bilivideo.com/video?sign=A%2fb&x=1";
        Assert.Equal(backup, PlaybackRouteSelector.Select(
            "https://a.mountaintoys.cn/video", [backup]));
    }
    [Fact]
    public void ExplicitRoutes_RespectNoP2PBeforePreferenceAndKeepAllRiskFallback()
    {
        const string primary = "https://a.mountaintoys.cn/video";
        const string backup = "https://upos-sz-mirrorcos.bilivideo.com/video";
        Assert.Equal(primary, PlaybackRouteSelector.Select(primary, [backup], PlaybackRoutePreference.Primary));
        Assert.Equal(backup, PlaybackRouteSelector.Select(primary, [backup], PlaybackRoutePreference.Primary, true));
        Assert.Equal(backup, PlaybackRouteSelector.Select("https://safe.example/video", [backup], PlaybackRoutePreference.Backup));
        Assert.Equal(primary, PlaybackRouteSelector.Select(primary, null, PlaybackRoutePreference.Backup, true));
    }
    [Theory]
    [InlineData((int)PlaybackRoutePreference.Ali, "upos-sz-mirrorali.bilivideo.com")]
    [InlineData((int)PlaybackRoutePreference.Tencent, "upos-sz-mirrorcos.bilivideo.com")]
    [InlineData((int)PlaybackRoutePreference.Huawei, "upos-sz-mirror08ct.bilivideo.com")]
    [InlineData((int)PlaybackRoutePreference.Overseas, "upos-hz-mirrorakam.akamaized.net")]
    public void Provider_SelectsOnlyMatchingSignedCandidate(int preferenceValue, string host)
    {
        var preference = (PlaybackRoutePreference)preferenceValue;
        var matching = "https://" + host + "/v?sign=Exact%2F";
        Assert.Equal(matching, PlaybackRouteSelector.Select("https://safe.example/v", [matching], preference));
        Assert.Equal("https://safe.example/v", PlaybackRouteSelector.Select("https://safe.example/v", ["https://" + host + ".evil.example/v"], preference));
    }

    [Theory]
    [InlineData("szbdyd.com")]
    [InlineData("mountaintoys.cn")]
    [InlineData("qrstuvwxyzab.com")]
    [InlineData("zhenidc.cn")]
    [InlineData("mcdn.bilivideo.cn")]
    [InlineData("mcdn.bilivideo.com")]
    [InlineData("pcdn.bilivideo.com")]
    [InlineData("gotcha.bilivideo.com")]
    public void Auto_RiskUsesDomainBoundaries(string domain)
    {
        const string safe = "https://safe.example/v";
        Assert.Equal(safe, PlaybackRouteSelector.Select("https://" + domain + "/v", [safe]));
        Assert.Equal(safe, PlaybackRouteSelector.Select("https://A." + domain.ToUpperInvariant() + ".:443/v", [safe]));
        var unrelated = "https://not" + domain + "/v";
        Assert.Equal(unrelated, PlaybackRouteSelector.Select(unrelated, [safe]));
        unrelated = "https://" + domain + ".example/v";
        Assert.Equal(unrelated, PlaybackRouteSelector.Select(unrelated, [safe]));
        Assert.Equal(safe, PlaybackRouteSelector.Select(safe + "?host=" + domain, ["https://a.mountaintoys.cn/v"])?.Split('?')[0]);
    }

    [Fact]
    public void EmptyInvalidDuplicateAndMissingPreferredCandidates_FallBackStably()
    {
        const string primary = "https://safe.example/v?sign=one";
        const string backup = "https://other.example/v?sign=two";
        Assert.Null(PlaybackRouteSelector.Select(null, null));
        Assert.Null(PlaybackRouteSelector.Select("invalid", ["", "file:///local", "ftp://example/v"]));
        Assert.Equal(backup, PlaybackRouteSelector.Select(primary, [primary, backup, backup], PlaybackRoutePreference.Backup));
        Assert.Equal(primary, PlaybackRouteSelector.Select(primary, [primary], PlaybackRoutePreference.Backup));
        Assert.Equal(primary, PlaybackRouteSelector.Select(primary, [backup], PlaybackRoutePreference.Ali));
        Assert.Equal(primary, PlaybackRouteSelector.Select(primary, [backup]));
        Assert.Equal(backup, PlaybackRouteSelector.Select("https://127.0.0.1/v", [backup]));
        Assert.Equal(backup, PlaybackRouteSelector.Select("https://[::1]/v", [backup]));
    }
    [Fact]
    public void Settings_DefaultAndEveryChoiceRoundTripWithoutPersistingUrls()
    {
        BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value = new();
        Assert.Equal(0, new BiliCopilot.UI.ViewModels.View.SettingsPageViewModel().PlaybackRouteIndex);
        foreach (var choice in new[] { 1, 2, 3, 4, 5, 6, 0 })
        {
            var settings = new BiliCopilot.UI.ViewModels.View.SettingsPageViewModel();
            settings.PlaybackRouteIndex = choice;
            Assert.Equal(choice, new BiliCopilot.UI.ViewModels.View.SettingsPageViewModel().PlaybackRouteIndex);
            Assert.Equal((PlaybackRoutePreference)choice, PlaybackRouteSettings.ReadPreference());
            Assert.IsType<int>(Assert.Single(BiliCopilot.UI.Toolkits.NavigationTestServices.Settings.Value).Value);
        }
        BiliCopilot.UI.Toolkits.SettingsToolkit.WriteLocalSetting(BiliCopilot.UI.Models.Constants.SettingNames.PlaybackRoutePreference, 999);
        Assert.Equal(PlaybackRoutePreference.Auto, PlaybackRouteSettings.ReadPreference());
    }
    [Fact]
    public void AllRiskFallbackAndProviderTies_PreserveApiOrder()
    {
        const string primary = "https://a.mountaintoys.cn/v";
        const string backup = "https://b.szbdyd.com/v";
        Assert.Equal(primary, PlaybackRouteSelector.Select(primary, [backup], PlaybackRoutePreference.Auto, true));
        Assert.Equal(primary, PlaybackRouteSelector.Select(primary, [backup], PlaybackRoutePreference.Huawei, true));
        Assert.Equal(backup, PlaybackRouteSelector.Select(primary, [backup], PlaybackRoutePreference.Backup, true));
        const string firstAli = "https://UPOS-SZ-MIRRORALI.BILIVIDEO.COM.:443/v?signature=A%2f";
        const string secondAli = "https://upos-sz-mirroralib.bilivideo.com/v";
        Assert.Equal(firstAli, PlaybackRouteSelector.Select("https://safe.example/v", [firstAli, secondAli], PlaybackRoutePreference.Ali));
        Assert.Equal(primary, PlaybackRouteSelector.Select(primary, [backup], (PlaybackRoutePreference)999));
    }
}
