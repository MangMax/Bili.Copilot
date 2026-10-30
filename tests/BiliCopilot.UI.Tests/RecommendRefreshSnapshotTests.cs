// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 撤销刷新用的快照。
/// </summary>
/// <remarks>
/// 快照必须复制列表内容，而不是持有页面里那个会被 Clear/Add 的集合引用，
/// 否则刷新后快照会被一起改写，撤销就恢复不回去了。
/// </remarks>
public class RecommendRefreshSnapshotTests
{
    [Fact]
    public void Capture_CopiesItemsSoLaterMutationsOfSourceDoNotLeak()
    {
        var source = new List<string> { "a", "b" };
        var snapshot = RecommendRefreshSnapshot<string>.Capture(source, 42);

        source.Clear();
        source.Add("c");

        Assert.Equal(new[] { "a", "b" }, snapshot.Items);
        Assert.Equal(42, snapshot.Offset);
    }

    [Fact]
    public void Capture_NullSource_ProducesEmptySnapshot()
    {
        var snapshot = RecommendRefreshSnapshot<string>.Capture(null, 0);

        Assert.Empty(snapshot.Items);
        Assert.Equal(0, snapshot.Offset);
    }

    [Fact]
    public void Capture_KeepsItemOrder()
    {
        var source = new[] { "x", "y", "z" };

        var snapshot = RecommendRefreshSnapshot<string>.Capture(source, 7);

        Assert.Equal(new[] { "x", "y", "z" }, snapshot.Items);
        Assert.Equal(7, snapshot.Offset);
    }
}
