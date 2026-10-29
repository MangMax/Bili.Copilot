// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public class DanmakuRedrawSessionTests
{
    [Fact]
    public void ReplacingPendingRedrawUsesOnlyTheLatestRendererCallback()
    {
        var queue = new Queue<Action>();
        var session = new DanmakuRedrawSession(callback => { queue.Enqueue(callback); return true; });
        var frames = new List<string>();
        session.Request(() => frames.Add("previous renderer"));
        session.Request(() => frames.Add("current renderer"));
        Assert.Single(queue);
        queue.Dequeue()();
        Assert.Equal(["current renderer"], frames);
    }

    [Fact]
    public void RejectedDispatchCanBeRetriedAndRedrawCanRequestAnotherFrame()
    {
        var queue = new Queue<Action>();
        var accepting = false;
        var session = new DanmakuRedrawSession(callback =>
        {
            if (!accepting) { return false; }
            queue.Enqueue(callback);
            return true;
        });
        var frames = new List<string>();
        session.Request(() => frames.Add("rejected"));
        accepting = true;
        session.Request(() =>
        {
            frames.Add("first");
            session.Request(() => frames.Add("tail"));
        });
        queue.Dequeue()();
        Assert.Equal(["first"], frames);
        queue.Dequeue()();
        Assert.Equal(["first", "tail"], frames);
    }

    [Fact]
    public void SwitchAndUnloadRejectOldCallbacksEvenAfterReplacementIsQueued()
    {
        var queue = new Queue<Action>();
        var session = new DanmakuRedrawSession(callback => { queue.Enqueue(callback); return true; });
        var frames = new List<string>();
        session.Request(() => frames.Add("disposed renderer"));
        var oldCallback = queue.Dequeue();
        session.Invalidate();
        session.Request(() => frames.Add("replacement"));

        oldCallback();
        Assert.Empty(frames);
        queue.Dequeue()();
        Assert.Equal(["replacement"], frames);

        session.Request(() => frames.Add("unloaded renderer"));
        session.Invalidate();
        queue.Dequeue()();
        Assert.Equal(["replacement"], frames);

        session.Request(() => frames.Add("reloaded renderer"));
        queue.Dequeue()();
        Assert.Equal(["replacement", "reloaded renderer"], frames);
    }

    [Fact]
    public void ReadyRendererQueuesImmediatelyAndBurstDrawsOnlyLatestState()
    {
        var queue = new Queue<Action>();
        var session = new DanmakuRedrawSession(callback => { queue.Enqueue(callback); return true; });
        var frames = new List<(int Position, bool Paused, string Style, string Content)>();
        var state = (Position: 12, Paused: false, Style: "small", Content: "old");

        session.Request(() => frames.Add(state));
        Assert.Single(queue); // Ready renderer needs no timer or inactive renderer.
        for (var i = 0; i < 100; i++)
        {
            session.Request(() => frames.Add(state));
        }

        state = (42, true, "large", "current");
        Assert.Empty(frames);
        Assert.Single(queue);
        queue.Dequeue()();
        Assert.Equal([(42, true, "large", "current")], frames);

        state = (43, false, "small", "next");
        session.Request(() => frames.Add(state));
        queue.Dequeue()();
        Assert.Equal((43, false, "small", "next"), frames[1]);
        Assert.Equal(2, frames.Count);
    }
}
