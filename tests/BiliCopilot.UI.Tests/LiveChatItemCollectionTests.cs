using System.Collections.Specialized;
using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 聊天集合：追加发一条通知，批量替换只发一次 Reset。
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
    public void ReplaceAll_RaisesOneResetNotification()
    {
        var collection = new LiveChatItemCollection<string>();
        collection.Append("old");
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);

        collection.ReplaceAll(["a", "b", "c"]);

        Assert.Equal([NotifyCollectionChangedAction.Reset], actions);
        Assert.Equal(["a", "b", "c"], collection);
    }

    [Fact]
    public void ReplaceAll_WithIdenticalContentRaisesNothing()
    {
        var collection = new LiveChatItemCollection<string>();
        collection.ReplaceAll(["a", "b"]);
        var raised = 0;
        collection.CollectionChanged += (_, _) => raised++;

        collection.ReplaceAll(["a", "b"]);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void ClearAll_RaisesOneResetAndIsIdempotent()
    {
        var collection = new LiveChatItemCollection<string>();
        collection.ReplaceAll(["a", "b"]);
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);

        collection.ClearAll();
        collection.ClearAll();

        Assert.Equal([NotifyCollectionChangedAction.Reset], actions);
        Assert.Empty(collection);
    }

    [Fact]
    public void ReplaceAll_HandlesFloodWithoutGrowing()
    {
        var collection = new LiveChatItemCollection<int>();
        var notifications = 0;
        collection.CollectionChanged += (_, _) => notifications++;

        for (var batch = 0; batch < 100; batch++)
        {
            collection.ReplaceAll(Enumerable.Range(batch * 500, 500));
        }

        Assert.Equal(500, collection.Count);
        Assert.Equal(100, notifications);
        Assert.Equal(49_999, collection[^1]);
    }
}
