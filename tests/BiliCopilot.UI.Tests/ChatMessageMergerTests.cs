// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class ChatMessageMergerTests
{
    [Fact]
    public void Merge_CombinesPagesWithoutDuplicatesInChronologicalOrder()
    {
        var current = new[]
        {
            new TestMessage(3, "c"),
            new TestMessage(4, "d"),
            new TestMessage(5, "e"),
        };
        var incoming = new[]
        {
            new TestMessage(1, "a"),
            new TestMessage(2, "b"),
            new TestMessage(3, "c"),
            new TestMessage(4, "d"),
            new TestMessage(5, "e"),
        };

        var result = ChatMessageMerger.Merge(current, incoming, p => p.Key);

        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, result.Items.Select(p => p.Key));
        Assert.Equal(new long[] { 1, 2 }, result.AddedItems.Select(p => p.Key));
    }

    [Fact]
    public void Merge_EmptyIncoming_KeepsCurrentListAndReportsNothingAdded()
    {
        var current = new[]
        {
            new TestMessage(10, "a"),
            new TestMessage(11, "b"),
        };

        var result = ChatMessageMerger.Merge(current, Array.Empty<TestMessage>(), p => p.Key);

        Assert.Equal(new long[] { 10, 11 }, result.Items.Select(p => p.Key));
        Assert.Empty(result.AddedItems);
    }

    [Fact]
    public void Merge_NullOrEmptyInputs_ReturnEmpty()
    {
        var result = ChatMessageMerger.Merge<TestMessage>(null, null, p => p.Key);

        Assert.Empty(result.Items);
        Assert.Empty(result.AddedItems);
    }

    [Fact]
    public void Merge_DuplicateKeysWithinOnePage_KeepSingleMessage()
    {
        var incoming = new[]
        {
            new TestMessage(1, "a"),
            new TestMessage(2, "first"),
            new TestMessage(2, "second"),
            new TestMessage(3, "c"),
        };

        var result = ChatMessageMerger.Merge<TestMessage>(null, incoming, p => p.Key);

        Assert.Equal(new long[] { 1, 2, 3 }, result.Items.Select(p => p.Key));
        Assert.Equal("second", result.Items.Single(p => p.Key == 2).Text);
    }

    [Fact]
    public void Merge_OutOfOrderPages_AreSortedChronologically()
    {
        var current = new[]
        {
            new TestMessage(100, "d"),
            new TestMessage(200, "e"),
        };
        var incoming = new[]
        {
            new TestMessage(200, "e"),
            new TestMessage(50, "a"),
            new TestMessage(100, "d"),
            new TestMessage(150, "c"),
        };

        var result = ChatMessageMerger.Merge(current, incoming, p => p.Key);

        Assert.Equal(new long[] { 50, 100, 150, 200 }, result.Items.Select(p => p.Key));
    }

    [Fact]
    public void Merge_RepeatedKeysFromLaterPage_DoNotAppearInAddedItems()
    {
        var current = new[]
        {
            new TestMessage(5, "e"),
        };
        var incoming = new[]
        {
            new TestMessage(4, "d"),
            new TestMessage(5, "e"),
        };

        var result = ChatMessageMerger.Merge(current, incoming, p => p.Key);

        Assert.Equal(new long[] { 4, 5 }, result.Items.Select(p => p.Key));
        var added = Assert.Single(result.AddedItems);
        Assert.Equal(4, added.Key);
    }

    [Fact]
    public void Merge_LaterPageWins_WhenSameKeyHasUpdatedContent()
    {
        var current = new[]
        {
            new TestMessage(7, "old content"),
        };
        var incoming = new[]
        {
            new TestMessage(7, "new content"),
        };

        var result = ChatMessageMerger.Merge(current, incoming, p => p.Key);

        Assert.Equal("new content", Assert.Single(result.Items).Text);
        Assert.Empty(result.AddedItems);
    }

    [Fact]
    public void Merge_AddedItemsAreChronological()
    {
        var current = new[]
        {
            new TestMessage(30, "c"),
        };
        var incoming = new[]
        {
            new TestMessage(30, "c"),
            new TestMessage(10, "a"),
            new TestMessage(20, "b"),
        };

        var result = ChatMessageMerger.Merge(current, incoming, p => p.Key);

        Assert.Equal(new long[] { 10, 20 }, result.AddedItems.Select(p => p.Key));
    }

    private sealed record TestMessage(long Key, string Text);
}
