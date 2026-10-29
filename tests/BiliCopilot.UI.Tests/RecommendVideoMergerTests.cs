// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class RecommendVideoMergerTests
{
    [Fact]
    public void Merge_PrependsNewBatchAndKeepsRemainingCurrentItemsInOrder()
    {
        var current = new[]
        {
            new TestVideo("c", "c"),
            new TestVideo("d", "d"),
            new TestVideo("e", "e"),
        };
        var incoming = new[]
        {
            new TestVideo("x", "x"),
            new TestVideo("c", "c-new"),
        };

        var result = RecommendVideoMerger.Merge(current, incoming, p => p.Id);

        Assert.Equal(new[] { "x", "c", "d", "e" }, result.Items.Select(p => p.Id));
        Assert.Equal("c-new", result.Items.Single(p => p.Id == "c").Title);
        var added = Assert.Single(result.AddedItems);
        Assert.Equal("x", added.Id);
    }

    [Fact]
    public void Merge_EmptyIncoming_KeepsCurrentListAndReportsNothingAdded()
    {
        var current = new[]
        {
            new TestVideo("a", "a"),
            new TestVideo("b", "b"),
        };

        var result = RecommendVideoMerger.Merge(current, Array.Empty<TestVideo>(), p => p.Id);

        Assert.Equal(new[] { "a", "b" }, result.Items.Select(p => p.Id));
        Assert.Empty(result.AddedItems);
    }

    [Fact]
    public void Merge_EmptyCurrent_ReturnsIncomingAndReportsAllAdded()
    {
        var incoming = new[]
        {
            new TestVideo("x", "x"),
            new TestVideo("y", "y"),
        };

        var result = RecommendVideoMerger.Merge(Array.Empty<TestVideo>(), incoming, p => p.Id);

        Assert.Equal(new[] { "x", "y" }, result.Items.Select(p => p.Id));
        Assert.Equal(new[] { "x", "y" }, result.AddedItems.Select(p => p.Id));
    }

    [Fact]
    public void Merge_NullInputs_AreTreatedAsEmpty()
    {
        var result = RecommendVideoMerger.Merge<TestVideo>(null, null, p => p.Id);

        Assert.Empty(result.Items);
        Assert.Empty(result.AddedItems);
    }

    [Fact]
    public void Merge_NullCurrent_ReturnsIncomingAndReportsAllAdded()
    {
        var incoming = new[]
        {
            new TestVideo("x", "x"),
        };

        var result = RecommendVideoMerger.Merge<TestVideo>(null, incoming, p => p.Id);

        Assert.Equal(new[] { "x" }, result.Items.Select(p => p.Id));
        Assert.Equal(new[] { "x" }, result.AddedItems.Select(p => p.Id));
    }

    [Fact]
    public void Merge_FullyOverlappingBatches_KeepIncomingOrderOnly()
    {
        var current = new[]
        {
            new TestVideo("a", "a"),
            new TestVideo("b", "b"),
        };
        var incoming = new[]
        {
            new TestVideo("b", "b-new"),
            new TestVideo("a", "a-new"),
        };

        var result = RecommendVideoMerger.Merge(current, incoming, p => p.Id);

        Assert.Equal(new[] { "b", "a" }, result.Items.Select(p => p.Id));
        Assert.Equal("b-new", result.Items[0].Title);
        Assert.Empty(result.AddedItems);
    }

    [Fact]
    public void Merge_DuplicateKeysWithinIncoming_KeepFirstOccurrenceOnly()
    {
        var incoming = new[]
        {
            new TestVideo("x", "first"),
            new TestVideo("y", "y"),
            new TestVideo("x", "second"),
        };

        var result = RecommendVideoMerger.Merge<TestVideo>(null, incoming, p => p.Id);

        Assert.Equal(new[] { "x", "y" }, result.Items.Select(p => p.Id));
        Assert.Equal("first", result.Items[0].Title);
        Assert.Equal(new[] { "x", "y" }, result.AddedItems.Select(p => p.Id));
    }

    [Fact]
    public void Merge_NullKeySelector_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => RecommendVideoMerger.Merge<TestVideo>(null, null, null!));
    }

    [Fact]
    public void Merge_DuplicateKeysWithinCurrent_KeepFirstOccurrenceOnly()
    {
        var current = new[]
        {
            new TestVideo("a", "first"),
            new TestVideo("b", "b"),
            new TestVideo("a", "second"),
        };

        var result = RecommendVideoMerger.Merge(current, Array.Empty<TestVideo>(), p => p.Id);

        Assert.Equal(new[] { "a", "b" }, result.Items.Select(p => p.Id));
        Assert.Equal("first", result.Items[0].Title);
        Assert.Empty(result.AddedItems);
    }

    private sealed record TestVideo(string Id, string Title);
}
