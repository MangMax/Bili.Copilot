// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockCategoryCatalogTests
{
    [Fact]
    public void All_ContainsEveryCategoryExactlyOnce()
    {
        var categories = SponsorBlockCategoryCatalog.All
            .Select(descriptor => descriptor.Category)
            .ToList();

        Assert.Equal(Enum.GetValues<SponsorBlockCategory>().Length, categories.Count);
        Assert.Equal(categories.Count, categories.Distinct().Count());
    }

    [Fact]
    public void EveryDescriptor_HasUniqueMetadata()
    {
        var descriptors = SponsorBlockCategoryCatalog.All;

        Assert.All(descriptors, descriptor =>
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.SettingName));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.BrushResourceKey));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.DisplayNameKey));
            Assert.Equal(0xFFu, descriptor.FallbackColor >> 24);
        });

        Assert.Equal(
            descriptors.Count,
            descriptors.Select(descriptor => descriptor.SettingName).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            descriptors.Count,
            descriptors.Select(descriptor => descriptor.BrushResourceKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            descriptors.Count,
            descriptors.Select(descriptor => descriptor.DisplayNameKey).Distinct(StringComparer.Ordinal).Count());

        // 只有未知类别没有接口名称.
        Assert.All(
            descriptors.Where(descriptor => descriptor.Category != SponsorBlockCategory.Unknown),
            descriptor => Assert.False(string.IsNullOrWhiteSpace(descriptor.ApiName)));
    }

    [Theory]
    [InlineData("sponsor", SponsorBlockCategory.Sponsor)]
    [InlineData("selfpromo", SponsorBlockCategory.SelfPromo)]
    [InlineData("exclusive_access", SponsorBlockCategory.ExclusiveAccess)]
    [InlineData("interaction", SponsorBlockCategory.Interaction)]
    [InlineData("poi_highlight", SponsorBlockCategory.PoiHighlight)]
    [InlineData("intro", SponsorBlockCategory.Intro)]
    [InlineData("outro", SponsorBlockCategory.Outro)]
    [InlineData("preview", SponsorBlockCategory.Preview)]
    [InlineData("padding", SponsorBlockCategory.Padding)]
    [InlineData("filler", SponsorBlockCategory.Filler)]
    [InlineData("music_offtopic", SponsorBlockCategory.MusicOfftopic)]
    [InlineData(" SELF_PROMO ", SponsorBlockCategory.Unknown)]
    [InlineData("something_new", SponsorBlockCategory.Unknown)]
    [InlineData("", SponsorBlockCategory.Unknown)]
    public void FromApiName_MapsKnownAndUnknownValues(string value, SponsorBlockCategory expected)
        => Assert.Equal(expected, SponsorBlockCategoryCatalog.FromApiName(value));

    [Theory]
    [InlineData(SponsorBlockCategory.Sponsor, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.SelfPromo, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.ExclusiveAccess, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.Intro, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.Outro, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.Interaction, SponsorBlockBehavior.ShowButton)]
    [InlineData(SponsorBlockCategory.PoiHighlight, SponsorBlockBehavior.ShowButton)]
    [InlineData(SponsorBlockCategory.Preview, SponsorBlockBehavior.None)]
    [InlineData(SponsorBlockCategory.Padding, SponsorBlockBehavior.None)]
    [InlineData(SponsorBlockCategory.Filler, SponsorBlockBehavior.None)]
    [InlineData(SponsorBlockCategory.MusicOfftopic, SponsorBlockBehavior.None)]
    [InlineData(SponsorBlockCategory.Unknown, SponsorBlockBehavior.None)]
    public void Descriptor_DefaultBehaviorMatchesDocumentedTable(
        SponsorBlockCategory category,
        SponsorBlockBehavior expected)
        => Assert.Equal(expected, SponsorBlockCategoryCatalog.Get(category).DefaultBehavior);
}
