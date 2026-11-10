using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

public sealed class LiveChatPipelineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void UserInput_InterruptsFollowingBeforeTheNextAppend()
    {
        var ownership = new LiveChatScrollOwnership();
        ownership.RequestBackToBottom();
        ownership.BeginUserInteraction();
        Assert.False(ownership.ReportAppend());
        Assert.True(ownership.ShouldShowBackToBottom);
        ownership.ReportUserScroll(900, 1000, 100);
        Assert.True(ownership.ReportAppend());
    }

    [Fact]
    public void Projection_RetainsIdentityAndNewestRowsAcrossMergeTrimAndDelete()
    {
        var session = new LiveChatSession(3, 1);
        var rows = new LiveChatItemCollection<LiveChatMessagePresentation>();
        void Sync() => LiveChatProjection.Synchronize(rows, session.Window.Items, m => new LiveChatMessagePresentation(m, null, Now));
        session.Apply(Message("first"), Now);
        Sync();
        var first = rows[0];
        session.Apply(Message("first"), Now);
        Sync();
        Assert.Same(first, rows[0]);
        Assert.Equal("×2", rows[0].MergeText);
        session.Apply(new LiveSuperChatEvent(new LiveSuperChatInformation("sc", "paid", 30, "u", 1, Now, Now.AddMinutes(1), null), "SC", Now), Now);
        Sync();
        session.Apply(new LiveSuperChatDeletedEvent(["sc"], "DELETE", Now), Now);
        Sync();
        Assert.Same(first, Assert.Single(rows));
        for (var i = 0; i < 10; i++)
        {
            session.Apply(Message("m" + i), Now);
            Sync();
            Assert.Equal(session.Window.Items.Select(m => m.Sequence), rows.Select(m => m.Message.Sequence));
            Assert.Equal("m" + i, rows[^1].Text);
        }
    }

    [Fact]
    public void InputFlood_IsBoundedAndCannotEvictSuperChatDeletion()
    {
        var buffer = new LiveChatEventBuffer(4, 2);
        var deletion = new LiveSuperChatDeletedEvent(["sc"], "DELETE", Now);
        buffer.Enqueue(deletion);
        for (var i = 0; i < 100; i++) buffer.Enqueue(Message("m" + i));
        Assert.Equal(96, buffer.Dropped);
        Assert.True(buffer.TryDequeue(out var control));
        Assert.Same(deletion, control);
        var texts = new List<string>();
        while (buffer.TryDequeue(out var value)) texts.Add(((LiveDanmakuReceivedEvent)value).Danmaku.Text);
        Assert.Equal(["m96", "m97", "m98", "m99"], texts);
    }

    [Fact]
    public void EnterEvents_AreDeliveredAndThrottledByTheModel()
    {
        var buffer = new LiveChatEventBuffer();
        var session = new LiveChatSession();
        for (var i = 0; i < 5; i++) buffer.Enqueue(new LiveUserEnteredEvent(new LiveUserEnteredInformation(i + 1, "u" + i), "ENTER", Now));
        while (buffer.TryDequeue(out var value)) session.Apply(value, Now);
        Assert.True(Assert.Single(session.Window.Items).IsEnterRoom);
    }

    private static LiveDanmakuReceivedEvent Message(string text)
        => new(new LiveDanmakuInformation(text, "#FFFFFF", "u", "", "", false) { UserId = 1 }, "DANMU_MSG", Now);
}
