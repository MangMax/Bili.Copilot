using System.Collections.Specialized;
using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 裁剪保留存活条目，不重置虚拟化视图。
/// </summary>
public sealed class LiveChatItemCollectionTests
{
    [Fact]
    public void Append_RaisesSingleAddNotification()
    {
        var collection = new LiveChatItemCollection<string>();
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);

        collection.Append("a");
        collection.Append("b");

        Assert.Equal([NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add], actions);
        Assert.Equal(["a", "b"], collection);
    }

    [Fact]
    public void PrefixTrimmingPreservesSurvivingRowsWithoutResettingTheView()
    {
        var collection = new LiveChatItemCollection<object>();
        var retained = new object();
        collection.Add(new object());
        collection.Add(new object());
        collection.Add(retained);
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);

        collection.RemoveFromStart(2);

        Assert.Same(retained, Assert.Single(collection));
        Assert.Equal([NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Remove], actions);
    }

    [Fact]
    public void ClearAll_RaisesOneResetAndIsIdempotent()
    {
        var collection = new LiveChatItemCollection<string>();
        collection.Add("a");
        collection.Add("b");
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);

        collection.ClearAll();
        collection.ClearAll();

        Assert.Equal([NotifyCollectionChangedAction.Reset], actions);
        Assert.Empty(collection);
    }

}
