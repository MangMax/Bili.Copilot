using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播聊天屏蔽规则：按用户 ID、昵称、关键词拦截，并分别计数。
/// </summary>
public sealed class LiveChatBlockRulesTests
{
    [Fact]
    public void Evaluate_BlocksByUserId_AndCountsIt()
    {
        var rules = new LiveChatBlockRules(userIds: [42L]);

        var kind = rules.Evaluate(42L, "someone", "hello");

        Assert.Equal(LiveChatBlockKind.UserId, kind);
        var counters = rules.Snapshot();
        Assert.Equal(1, counters.ByUserId);
        Assert.Equal(0, counters.ByUserName);
        Assert.Equal(0, counters.ByKeyword);
        Assert.Equal(1, counters.Total);
    }

    [Fact]
    public void Evaluate_BlocksByNickname_IgnoringCaseAndOuterWhitespace()
    {
        var rules = new LiveChatBlockRules(userNames: ["  SpamBot "]);

        Assert.Equal(LiveChatBlockKind.UserName, rules.Evaluate(1L, "spambot", "hi"));
        Assert.Equal(LiveChatBlockKind.None, rules.Evaluate(2L, "spambot2", "hi"));
        Assert.Equal(1, rules.Snapshot().ByUserName);
    }

    [Fact]
    public void Evaluate_BlocksByKeyword_AsCaseInsensitiveSubstring()
    {
        var rules = new LiveChatBlockRules(keywords: ["广告"]);

        Assert.Equal(LiveChatBlockKind.Keyword, rules.Evaluate(1L, "u", "这里有广告，快走"));
        Assert.Equal(LiveChatBlockKind.None, rules.Evaluate(1L, "u", "没有推广"));
        Assert.Equal(1, rules.Snapshot().ByKeyword);
    }

    [Fact]
    public void Evaluate_PrefersUserIdOverNameOverKeyword()
    {
        var rules = new LiveChatBlockRules(userIds: [7L], userNames: ["banned"], keywords: ["bad"]);

        Assert.Equal(LiveChatBlockKind.UserId, rules.Evaluate(7L, "banned", "bad"));
        Assert.Equal(LiveChatBlockKind.UserName, rules.Evaluate(8L, "banned", "bad"));
        Assert.Equal(LiveChatBlockKind.Keyword, rules.Evaluate(9L, "ok", "bad"));

        var counters = rules.Snapshot();
        Assert.Equal(1, counters.ByUserId);
        Assert.Equal(1, counters.ByUserName);
        Assert.Equal(1, counters.ByKeyword);
        Assert.Equal(3, counters.Total);
    }

    [Fact]
    public void Evaluate_DoesNotCountUnblockedMessages()
    {
        var rules = new LiveChatBlockRules(keywords: ["x"]);

        Assert.Equal(LiveChatBlockKind.None, rules.Evaluate(1L, "u", "clean"));
        Assert.Equal(0, rules.Snapshot().Total);
    }

    [Fact]
    public void EmptyRules_BlockNothing_AndAreReportedAsEmpty()
    {
        var rules = new LiveChatBlockRules();

        Assert.True(rules.IsEmpty);
        Assert.Equal(LiveChatBlockKind.None, rules.Evaluate(1L, "u", "text"));
        Assert.Equal(default, rules.Snapshot());
    }

    [Fact]
    public void Replace_SwapsRulesAndKeepsExistingCounters()
    {
        var rules = new LiveChatBlockRules(keywords: ["ad"]);
        _ = rules.Evaluate(1L, "u", "ad");

        rules.Replace(userIds: [5L], userNames: [], keywords: []);

        Assert.False(rules.IsEmpty);
        Assert.Equal(LiveChatBlockKind.UserId, rules.Evaluate(5L, "u", "clean"));
        Assert.Equal(LiveChatBlockKind.None, rules.Evaluate(1L, "u", "ad"));
        Assert.Equal(1, rules.Snapshot().ByKeyword);
        Assert.Equal(1, rules.Snapshot().ByUserId);
    }

    [Fact]
    public void Evaluate_IgnoresNullAndEmptyInputs()
    {
        var rules = new LiveChatBlockRules(userNames: ["x"], keywords: ["y"]);

        Assert.Equal(LiveChatBlockKind.None, rules.Evaluate(0L, null, null));
        Assert.Equal(LiveChatBlockKind.None, rules.Evaluate(0L, string.Empty, string.Empty));
    }

    [Fact]
    public void Counters_AreSafeUnderConcurrentEvaluation()
    {
        var rules = new LiveChatBlockRules(keywords: ["flood"]);

        Parallel.For(0, 500, index => rules.Evaluate(index, "u", "flood " + index));

        var counters = rules.Snapshot();
        Assert.Equal(500, counters.ByKeyword);
        Assert.Equal(500, counters.Total);
    }
}
