using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class RecommendFeedMergerTests
{
    [Fact]
    public void InterleavesMobileFirstAndDropsDuplicates()
    {
        var merged = RecommendFeedMerger.Merge(
            new[] { "a", "b", "c" },
            new[] { "x", "b", "y" },
            p => p);

        Assert.Equal(new[] { "a", "x", "b", "c", "y" }, merged);
    }

    [Fact]
    public void EmptySideReturnsTheOther()
    {
        Assert.Equal(new[] { "a" }, RecommendFeedMerger.Merge(new[] { "a" }, [], p => p));
        Assert.Equal(new[] { "b" }, RecommendFeedMerger.Merge([], new[] { "b" }, p => p));
        Assert.Empty(RecommendFeedMerger.Merge<string>([], [], p => p));
    }

    [Fact]
    public void LongerSideIsAppendedAfterInterleaving()
    {
        var merged = RecommendFeedMerger.Merge(
            new[] { "a", "b", "c", "d" },
            new[] { "x" },
            p => p);

        Assert.Equal(new[] { "a", "x", "b", "c", "d" }, merged);
    }
}
