// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class PublisherNameMergerTests
{
    [Fact]
    public void Merge_WithEmptyCollaborators_ReturnsPrimary()
        => Assert.Equal("主投稿人", PublisherNameMerger.Merge("主投稿人", []));

    [Fact]
    public void Merge_WithNullCollaborators_ReturnsPrimary()
        => Assert.Equal("主投稿人", PublisherNameMerger.Merge("主投稿人", null));

    [Fact]
    public void Merge_WithOnlyPrimary_ReturnsPrimary()
        => Assert.Equal("主投稿人", PublisherNameMerger.Merge("主投稿人", ["主投稿人"]));

    [Fact]
    public void Merge_WithMultipleDistinctNames_JoinsInOrder()
        => Assert.Equal("甲、乙、丙", PublisherNameMerger.Merge("甲", ["乙", "丙"]));

    [Fact]
    public void Merge_WithCollaboratorListIncludingPrimary_DoesNotRepeatPrimary()
        => Assert.Equal("甲、乙、丙", PublisherNameMerger.Merge("甲", ["乙", "甲", "丙", "乙"]));

    [Fact]
    public void Merge_ComparesNamesCaseSensitively()
        => Assert.Equal("Alice、alice", PublisherNameMerger.Merge("Alice", ["alice"]));

    [Fact]
    public void Merge_IgnoresNullOrBlankCollaborators()
        => Assert.Equal("甲、乙", PublisherNameMerger.Merge("甲", [null, string.Empty, "  ", "乙"]));

    [Fact]
    public void Merge_TrimsWhitespaceFromNames()
        => Assert.Equal("甲、乙", PublisherNameMerger.Merge(" 甲 ", [" 乙\t"]));

    [Fact]
    public void Merge_WithNoPrimary_ReturnsOnlyCollaborators()
        => Assert.Equal("乙、丙", PublisherNameMerger.Merge(null, ["乙", "丙"]));

    [Fact]
    public void Merge_WithNoNames_ReturnsEmptyString()
        => Assert.Equal(string.Empty, PublisherNameMerger.Merge(" ", [null, "  "]));
}
