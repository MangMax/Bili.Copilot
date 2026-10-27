// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockSegmentParserTests
{
    [Fact]
    public void Parse_FlatArray_ReadsSegmentFields()
    {
        const string json = """
            [
              {
                "cid": "168885122",
                "category": "sponsor",
                "actionType": "skip",
                "segment": [300.019, 600.014],
                "UUID": "d0f1e2a3",
                "videoDuration": 1801
              }
            ]
            """;

        var segments = SponsorBlockSegmentParser.Parse(json, "168885122");

        var segment = Assert.Single(segments);
        Assert.Equal(SponsorBlockCategory.Sponsor, segment.Category);
        Assert.Equal(300.019, segment.Start, 3);
        Assert.Equal(600.014, segment.End, 3);
        Assert.Equal("d0f1e2a3", segment.Uuid);
        Assert.Equal(SponsorBlockActionType.Skip, segment.ActionType);
    }

    [Fact]
    public void Parse_KeepsOnlySegmentsOfExpectedCid()
    {
        const string json = """
            [
              { "cid": "111", "category": "sponsor", "actionType": "skip", "segment": [10, 20], "UUID": "a" },
              { "cid": 222, "category": "intro", "actionType": "skip", "segment": [30, 40], "UUID": "b" },
              { "category": "outro", "actionType": "skip", "segment": [50, 60], "UUID": "c" }
            ]
            """;

        var segments = SponsorBlockSegmentParser.Parse(json, "222");

        Assert.Equal(2, segments.Count);
        Assert.Equal(30, segments[0].Start);
        Assert.Equal(50, segments[1].Start);
    }

    [Theory]
    [InlineData("poi")]
    [InlineData("full")]
    [InlineData("something_new")]
    public void Parse_IgnoresOutOfScopeActions(string actionType)
    {
        var json = $$"""
            [
              { "category": "sponsor", "actionType": "{{actionType}}", "segment": [10, 20], "UUID": "a" }
            ]
            """;

        Assert.Empty(SponsorBlockSegmentParser.Parse(json));
    }

    [Theory]
    [InlineData("mute", SponsorBlockActionType.Mute)]
    [InlineData("MUTE", SponsorBlockActionType.Mute)]
    [InlineData("skip", SponsorBlockActionType.Skip)]
    [InlineData("", SponsorBlockActionType.Skip)]
    public void Parse_KeepsSupportedActionType(string actionType, SponsorBlockActionType expected)
    {
        var json = $$"""
            [
              { "category": "sponsor", "actionType": "{{actionType}}", "segment": [10, 20], "UUID": "a" }
            ]
            """;

        var segment = Assert.Single(SponsorBlockSegmentParser.Parse(json));
        Assert.Equal(expected, segment.ActionType);
    }

    [Fact]
    public void Parse_MissingActionType_DefaultsToSkip()
    {
        const string json = """
            [
              { "category": "sponsor", "segment": [10, 20], "UUID": "a" }
            ]
            """;

        var segment = Assert.Single(SponsorBlockSegmentParser.Parse(json));
        Assert.Equal(SponsorBlockActionType.Skip, segment.ActionType);
    }

    [Fact]
    public void Parse_IgnoresInvalidRanges()
    {
        const string json = """
            [
              { "category": "sponsor", "actionType": "skip", "segment": [20, 10], "UUID": "a" },
              { "category": "sponsor", "actionType": "skip", "segment": [-1, 10], "UUID": "b" },
              { "category": "sponsor", "actionType": "skip", "segment": [10], "UUID": "c" },
              { "category": "sponsor", "actionType": "skip", "segment": [30, 40], "UUID": "d" }
            ]
            """;

        var segment = Assert.Single(SponsorBlockSegmentParser.Parse(json));
        Assert.Equal("d", segment.Uuid);
    }

    [Fact]
    public void Parse_MapsKnownAndUnknownCategories()
    {
        const string json = """
            [
              { "category": "poi_highlight", "actionType": "skip", "segment": [1, 2] },
              { "category": "something_new", "actionType": "skip", "segment": [3, 4] }
            ]
            """;

        var segments = SponsorBlockSegmentParser.Parse(json);

        Assert.Equal(SponsorBlockCategory.PoiHighlight, segments[0].Category);
        Assert.Equal(SponsorBlockCategory.Unknown, segments[1].Category);
    }

    [Fact]
    public void Parse_SortsByStartTime()
    {
        const string json = """
            [
              { "category": "intro", "actionType": "skip", "segment": [80, 90] },
              { "category": "sponsor", "actionType": "skip", "segment": [10, 20] }
            ]
            """;

        var segments = SponsorBlockSegmentParser.Parse(json);

        Assert.Equal(10, segments[0].Start);
        Assert.Equal(80, segments[1].Start);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{ not json")]
    [InlineData("{\"unexpected\":true}")]
    public void Parse_InvalidOrEmptyPayload_ReturnsEmpty(string json)
    {
        Assert.Empty(SponsorBlockSegmentParser.Parse(json));
    }
}
