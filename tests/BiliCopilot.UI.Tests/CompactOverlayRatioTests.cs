using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class CompactOverlayRatioTests
{
    [Fact]
    public void PersistedSettingKeyAndLocalizedToggleTextAreAvailable()
    {
        Assert.Equal("AutoCompactOverlayRatio", Models.Constants.SettingNames.AutoCompactOverlayRatio.ToString());
        var resources = ResourcesFileHelper.LoadResources();
        foreach (var key in new[] { "AutoCompactOverlayRatio", "AutoCompactOverlayRatioDescription" })
        {
            var entry = Assert.Single(resources, data => (string?)data.Attribute("name") == key);
            Assert.False(string.IsNullOrWhiteSpace(entry.Element("value")?.Value));
        }
    }

    [Theory]
    [InlineData(1920, 1080, 16d / 9d)]
    [InlineData(4, 3, 16d / 9d)]
    [InlineData(1, 1, 16d / 9d)]
    [InlineData(1080, 1920, 9d / 16d)]
    [InlineData(9, 16, 9d / 16d)]
    public void SourceOrientationSelectsOneOfTwoForms(double width, double height, double expected)
        => Assert.Equal(expected, CompactOverlayLayout.SelectForm(width / height));

    [Theory]
    [InlineData(640, 360, 16d / 9d, 640, 360)]
    [InlineData(640, 360, 9d / 16d, 360, 640)]
    [InlineData(500, 500, 16d / 9d, 500, 281)]
    [InlineData(500, 500, 9d / 16d, 281, 500)]
    public void FitKeepsTheLongerClientEdgeAndOneOfTwoForms(
        int width, int height, double form, int expectedWidth, int expectedHeight)
    {
        var area = new WindowRect(0, 0, 1920, 1040);
        var result = CompactOverlayLayout.Fit(new WindowPoint(100, 80), new WindowRect(100, 80, width, height),
            0, 0, area, form);
        Assert.Equal(new WindowRect(100, 80, expectedWidth, expectedHeight), result);
    }

    [Fact]
    public void FitScalesDownToTheWorkAreaAndRecoversAFullyHiddenOrigin()
    {
        var area = new WindowRect(0, 0, 500, 400);
        var result = CompactOverlayLayout.Fit(new WindowPoint(4000, 3000), new WindowRect(4000, 3000, 640, 360),
            0, 0, area, 16d / 9d);
        Assert.Equal(new WindowRect(0, 119, 500, 281), result);
    }

    [Theory]
    [InlineData(-1800, 400, -1920, 1920, -1800)]
    [InlineData(-100, 400, -1920, 1920, -100)]
    [InlineData(3000, 400, 0, 1920, 1520)]
    [InlineData(0, 400, 0, 1920, 0)]
    public void RecoverAxisKeepsAnyVisiblePartAndOnlyRecoversAFullyHiddenAxis(
        int position, int size, int areaStart, int areaSize, int expected)
        => Assert.Equal(expected, CompactOverlayLayout.RecoverAxis(position, size, areaStart, areaSize));

    [Fact]
    public void OlderMediaAndInvalidDimensionsCannotChangeTheKnownForm()
    {
        using var ratio = CreateRatio();
        var first = ratio.BeginMedia();
        ratio.UpdateMedia(first, 1920, 1080);
        Assert.Equal(CompactOverlayLayout.LandscapeRatio, ratio.Form);

        var second = ratio.BeginMedia();
        ratio.UpdateMedia(second, 1080, 1920);
        ratio.UpdateMedia(first, 1920, 1080);
        ratio.UpdateMedia(second, double.NaN, 1080);
        ratio.UpdateMedia(second, 1080, 0);
        ratio.UpdateMedia(second, 1080, double.PositiveInfinity);
        Assert.Equal(CompactOverlayLayout.PortraitRatio, ratio.Form);
    }

    [Fact]
    public void SameFormDoesNotRaiseButANewMediaGenerationDoes()
    {
        using var ratio = CreateRatio();
        var generation = ratio.BeginMedia();
        var changes = 0;
        ratio.Changed += (_, _) => changes++;
        ratio.UpdateMedia(generation, 1920, 1080);
        ratio.UpdateMedia(generation, 2560, 1440);
        Assert.Equal(1, changes);

        generation = ratio.BeginMedia();
        ratio.UpdateMedia(generation, 1920, 1080);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void GlobalPreferencePersistsAndRepeatedEnableDoesNotRaise()
    {
        bool? stored = null;
        var preference = new CompactOverlayRatioPreference(() => stored, value => stored = value);
        var changes = 0;
        preference.Changed += (_, _) => changes++;
        Assert.True(preference.Enabled);

        preference.SetEnabled(false);
        Assert.False(new CompactOverlayRatioPreference(() => stored, _ => { }).Enabled);
        preference.SetEnabled(false);
        Assert.Equal(1, changes);

        preference.SetEnabled(true);
        Assert.Equal(2, changes);
        Assert.True(stored);
    }

    private static CompactOverlayRatio CreateRatio()
        => new(new CompactOverlayRatioPreference(() => null, _ => { }));
}
