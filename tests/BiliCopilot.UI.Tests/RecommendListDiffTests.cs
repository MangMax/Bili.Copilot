// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 推荐列表最小差异的计算与应用.
/// </summary>
/// <remarks>
/// 核心目的：应用差异后列表内容与顺序等于目标列表，且位置未变的条目保持原引用，
/// 这样 <c>ItemsRepeater</c> 不会销毁并重建这些条目的卡片容器。
/// </remarks>
public class RecommendListDiffTests
{
    [Fact]
    public void Apply_IdenticalLists_KeepsEveryItemReference()
    {
        var a = new TestVideo("a", "A");
        var b = new TestVideo("b", "B");
        var current = new List<TestVideo> { a, b };

        // 目标列表用内容相同但实例不同的条目，验证「不动」是按引用保留当前条目。
        var target = new List<TestVideo> { new("a", "A"), new("b", "B") };

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Empty(diff.RemovedIndices);
        Assert.Empty(diff.Insertions);
        Assert.Same(a, current[0]);
        Assert.Same(b, current[1]);
    }

    [Fact]
    public void Apply_InsertionsOnly_InsertsNewItemsAndKeepsTheRest()
    {
        var a = new TestVideo("a", "A");
        var b = new TestVideo("b", "B");
        var x = new TestVideo("x", "X");
        var current = new List<TestVideo> { a, b };

        // 增量刷新：新批次前插，旧的条目原样留在后面。
        var target = new List<TestVideo> { x, a, b };

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Empty(diff.RemovedIndices);
        var insertion = Assert.Single(diff.Insertions);
        Assert.Equal(0, insertion.Index);
        Assert.Same(x, insertion.Item);
        Assert.Equal(new[] { "x", "a", "b" }, current.Select(p => p.Id));
        Assert.Same(a, current[1]);
        Assert.Same(b, current[2]);
    }

    [Fact]
    public void Apply_RemovalsOnly_RemovesMissingItemsAndKeepsTheRest()
    {
        var a = new TestVideo("a", "A");
        var b = new TestVideo("b", "B");
        var c = new TestVideo("c", "C");
        var current = new List<TestVideo> { a, b, c };
        var target = new List<TestVideo> { a, c };

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Equal(new[] { 1 }, diff.RemovedIndices);
        Assert.Empty(diff.Insertions);
        Assert.Equal(new[] { "a", "c" }, current.Select(p => p.Id));
        Assert.Same(a, current[0]);
        Assert.Same(c, current[1]);
    }

    [Fact]
    public void Apply_InsertionsAndRemovals_AppliesBothInSafeOrder()
    {
        var a = new TestVideo("a", "A");
        var b = new TestVideo("b", "B");
        var c = new TestVideo("c", "C");
        var x = new TestVideo("x", "X");
        var y = new TestVideo("y", "Y");
        var current = new List<TestVideo> { a, b, c };
        var target = new List<TestVideo> { x, b, y };

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);

        // 删除按倒序，插入按升序，视图模型可以依次直接执行。
        Assert.Equal(new[] { 2, 0 }, diff.RemovedIndices);
        Assert.Equal(new[] { 0, 2 }, diff.Insertions.Select(p => p.Index));

        RecommendListDiff.Apply(current, diff);

        Assert.Equal(new[] { "x", "b", "y" }, current.Select(p => p.Id));
        Assert.Same(b, current[1]);
    }

    [Fact]
    public void Apply_EmptyCurrent_InsertsEverythingInOrder()
    {
        var target = new List<TestVideo> { new("x", "X"), new("y", "Y") };
        var current = new List<TestVideo>();

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Empty(diff.RemovedIndices);
        Assert.Equal(new[] { 0, 1 }, diff.Insertions.Select(p => p.Index));
        Assert.Equal(new[] { "x", "y" }, current.Select(p => p.Id));
    }

    [Fact]
    public void Apply_EmptyTarget_RemovesEverythingInDescendingOrder()
    {
        var current = new List<TestVideo> { new("a", "A"), new("b", "B") };
        var target = new List<TestVideo>();

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Equal(new[] { 1, 0 }, diff.RemovedIndices);
        Assert.Empty(diff.Insertions);
        Assert.Empty(current);
    }

    [Fact]
    public void Apply_DuplicateKeys_AreMatchedByOccurrenceAndKept()
    {
        var first = new TestVideo("a", "A1");
        var second = new TestVideo("a", "A2");
        var current = new List<TestVideo> { first, second };

        // 键相同的条目按出现次序匹配；键序列没变时不做任何改动。
        var target = new List<TestVideo> { new("a", "A1"), new("a", "A2") };

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Empty(diff.RemovedIndices);
        Assert.Empty(diff.Insertions);
        Assert.Equal(new[] { "a", "a" }, current.Select(p => p.Id));
        Assert.Same(first, current[0]);
        Assert.Same(second, current[1]);
    }

    [Fact]
    public void Apply_TargetHasFewerDuplicateOccurrences_RemovesOnlyTheSurplus()
    {
        var first = new TestVideo("a", "A1");
        var second = new TestVideo("a", "A2");
        var b = new TestVideo("b", "B");
        var current = new List<TestVideo> { first, second, b };
        var target = new List<TestVideo> { first, b };

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Equal(new[] { 1 }, diff.RemovedIndices);
        Assert.Empty(diff.Insertions);
        Assert.Equal(new[] { "a", "b" }, current.Select(p => p.Id));
        Assert.Same(first, current[0]);
        Assert.Same(b, current[1]);
    }

    [Fact]
    public void Apply_TargetHasMoreDuplicateOccurrences_InsertsTheExtraOne()
    {
        var a = new TestVideo("a", "A");
        var current = new List<TestVideo> { a };
        var extra = new TestVideo("a", "A-extra");
        var target = new List<TestVideo> { a, extra };

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Empty(diff.RemovedIndices);
        var insertion = Assert.Single(diff.Insertions);
        Assert.Equal(1, insertion.Index);
        Assert.Same(extra, insertion.Item);
        Assert.Same(a, current[0]);
    }

    [Fact]
    public void Apply_MovedItems_AppliesTargetOrderAndKeepsUnchangedReferences()
    {
        var a = new TestVideo("a", "A");
        var b = new TestVideo("b", "B");
        var c = new TestVideo("c", "C");
        var current = new List<TestVideo> { a, b, c };
        var target = new List<TestVideo> { c, a, b };

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Equal(new[] { "c", "a", "b" }, current.Select(p => p.Id));

        // a、b 的相对顺序没变，保留原引用；c 移到最前面。
        Assert.Same(c, current[0]);
        Assert.Same(a, current[1]);
        Assert.Same(b, current[2]);
    }

    [Fact]
    public void Apply_ReversedOrder_StillMatchesTargetOrder()
    {
        var a = new TestVideo("a", "A");
        var b = new TestVideo("b", "B");
        var c = new TestVideo("c", "C");
        var current = new List<TestVideo> { a, b, c };
        var target = new List<TestVideo> { c, b, a };

        var diff = RecommendListDiff.Compute(current, target, p => p.Id);
        RecommendListDiff.Apply(current, diff);

        Assert.Equal(new[] { "c", "b", "a" }, current.Select(p => p.Id));

        // 至少有一个条目可以原地保留。
        Assert.Same(c, current[0]);
    }

    [Fact]
    public void Compute_NullLists_ProduceNoChanges()
    {
        var diff = RecommendListDiff.Compute<TestVideo>(null, null, p => p.Id);

        Assert.Empty(diff.RemovedIndices);
        Assert.Empty(diff.Insertions);
    }

    [Fact]
    public void Apply_ToObservableCollection_RaisesOnlyIncrementalEvents()
    {
        var old1 = new TestVideo("old1", "Old 1");
        var old2 = new TestVideo("old2", "Old 2");
        var fresh = new TestVideo("fresh", "Fresh");
        var current = new ObservableCollection<TestVideo> { old1, old2 };
        var actions = new List<NotifyCollectionChangedAction>();
        current.CollectionChanged += (_, e) => actions.Add(e.Action);

        RecommendListDiff.Apply(current, new[] { fresh, old1, old2 }, p => p.Id);

        // 只有一次 Add，没有 Reset：ItemsRepeater 不会重建已有卡片。
        Assert.Equal(new[] { NotifyCollectionChangedAction.Add }, actions);
        Assert.Equal(new[] { "fresh", "old1", "old2" }, current.Select(p => p.Id));
        Assert.Same(old1, current[1]);
        Assert.Same(old2, current[2]);
    }

    private sealed record TestVideo(string Id, string Name);
}
