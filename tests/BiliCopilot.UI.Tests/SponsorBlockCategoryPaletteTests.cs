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
