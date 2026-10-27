// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockCategoryPaletteTests
{
    [Fact]
    public void GetBrushKey_ReturnsDistinctKeyForEveryCategory()
    {
        var keys = Enum.GetValues<SponsorBlockCategory>()
            .Select(SponsorBlockCategoryPalette.GetBrushKey)
            .ToList();

        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(SponsorBlockCategory.Sponsor, "SponsorBlockMarkerSponsorBrush")]
    [InlineData(SponsorBlockCategory.MusicOfftopic, "SponsorBlockMarkerMusicOfftopicBrush")]
    [InlineData(SponsorBlockCategory.Unknown, "SponsorBlockMarkerUnknownBrush")]
    public void GetBrushKey_UsesResourceNamingConvention(
        SponsorBlockCategory category,
        string expected)
        => Assert.Equal(expected, SponsorBlockCategoryPalette.GetBrushKey(category));

    [Theory]
    [InlineData(SponsorBlockCategory.Sponsor, 0xFFD88CA8)]
    [InlineData(SponsorBlockCategory.SelfPromo, 0xFFD9A06D)]
    [InlineData(SponsorBlockCategory.ExclusiveAccess, 0xFF73B39E)]
    [InlineData(SponsorBlockCategory.Interaction, 0xFFA996D6)]
    [InlineData(SponsorBlockCategory.PoiHighlight, 0xFFD68FB5)]
    [InlineData(SponsorBlockCategory.Intro, 0xFF72ADBA)]
    [InlineData(SponsorBlockCategory.Outro, 0xFF8798CF)]
    [InlineData(SponsorBlockCategory.Preview, 0xFF7DA8C1)]
    [InlineData(SponsorBlockCategory.Padding, 0xFFA4A4A4)]
    [InlineData(SponsorBlockCategory.Filler, 0xFF9D8FBE)]
    [InlineData(SponsorBlockCategory.MusicOfftopic, 0xFFCC9D62)]
    [InlineData(SponsorBlockCategory.Unknown, 0xFFBC898B)]
    public void GetFallbackColor_MatchesDarkThemePalette(
        SponsorBlockCategory category,
        uint expected)
        => Assert.Equal(expected, SponsorBlockCategoryPalette.GetFallbackColor(category));

    [Fact]
    public void GetFallbackColor_IsOpaqueForEveryCategory()
    {
        foreach (var category in Enum.GetValues<SponsorBlockCategory>())
        {
            var argb = SponsorBlockCategoryPalette.GetFallbackColor(category);

            Assert.Equal(0xFFu, argb >> 24);
        }
    }
}
