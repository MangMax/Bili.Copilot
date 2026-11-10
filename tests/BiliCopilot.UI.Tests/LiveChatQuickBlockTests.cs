using BiliCopilot.UI.Live;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

public sealed class LiveChatQuickBlockTests
{
    [Fact]
    public void SelectedPhrasePreservesSpacesAndPunctuationAndExistingRules()
    {
        var text = LiveChatBlockInput.Add("已有规则", "  hello, world; 广告  ");
        text = LiveChatBlockInput.Add(text, "HELLO, WORLD; 广告");
        Assert.Equal(["已有规则", "hello, world; 广告"], LiveChatBlockInput.Lines(text));
        var rules = new LiveChatBlockRules(keywords: LiveChatBlockInput.Lines(text));
        Assert.Equal(LiveChatBlockKind.Keyword, rules.Evaluate(1, "u", "前缀 hello, world; 广告 后缀"));
        Assert.Equal(LiveChatBlockKind.None, rules.Evaluate(1, "u", "hello"));
    }

    [Fact]
    public void IdInputKeepsExistingIdsAndRejectsMissingOrInvalidIds()
        => Assert.Equal([12L, 34L], LiveChatBlockInput.UserIds("12, 34\n12;0;-1;invalid"));

    [Fact]
    public void ApplyingUserRuleRemovesExistingMessagesAndBlocksFutureMessages()
    {
        var now = DateTimeOffset.UnixEpoch;
        var session = new LiveChatSession();
        LiveDanmakuReceivedEvent Message(long id, string text) => new(
            new LiveDanmakuInformation(text, "#FFFFFF", "user", "", "", false) { UserId = id }, "DANMU_MSG", now);
        session.Apply(Message(12, "remove"), now);
        session.Apply(Message(34, "keep"), now);
        var retained = session.Window.Items[1];
        session.UpdateBlockRules([12], null, null);
        Assert.Same(retained, Assert.Single(session.Window.Items));
        session.Apply(Message(12, "also blocked"), now);
        Assert.Single(session.Window.Items);
        Assert.Equal(2, session.BlockCounters.ByUserId);
    }

    [Fact]
    public void ApplyingKeywordRuleRemovesMatchingRowsButKeepsSystemMessages()
    {
        var now = DateTimeOffset.UnixEpoch;
        var session = new LiveChatSession();
        session.Apply(new LiveConnectionStateChangedEvent(LiveDanmakuConnectionState.Connected, null, now), now);
        session.Apply(new LiveDanmakuReceivedEvent(new LiveDanmakuInformation("some phrase", "#FFFFFF", "u", "", "", false), "DANMU_MSG", now), now);
        session.UpdateBlockRules(null, null, ["some phrase"]);
        Assert.Equal(LiveChatMessageKind.System, Assert.Single(session.Window.Items).Kind);
        Assert.Equal(1, session.BlockCounters.ByKeyword);
    }
}
