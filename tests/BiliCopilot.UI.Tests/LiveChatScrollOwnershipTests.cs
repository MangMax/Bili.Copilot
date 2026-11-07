using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 滚动所有权：自动跟随 / 用户回看 / 回到底部三种状态的迁移。
/// </summary>
public sealed class LiveChatScrollOwnershipTests
{
    [Fact]
    public void StartsFollowing()
    {
        var ownership = new LiveChatScrollOwnership();

        Assert.True(ownership.IsFollowing);
        Assert.False(ownership.ShouldShowBackToBottom);
    }

    [Fact]
    public void UserScrollsUp_StopsFollowingAndShowsAffordance()
    {
        var ownership = new LiveChatScrollOwnership();

        var changed = ownership.ReportUserScroll(offset: 100, extent: 1000, viewport: 300);

        Assert.True(changed);
        Assert.False(ownership.IsFollowing);
        Assert.True(ownership.ShouldShowBackToBottom);
    }

    [Fact]
    public void UserScrollsBackToBottom_ResumesFollowingAndHidesAffordance()
    {
        var ownership = new LiveChatScrollOwnership();
        ownership.ReportUserScroll(offset: 100, extent: 1000, viewport: 300);

        ownership.ReportUserScroll(offset: 700, extent: 1000, viewport: 300);

        Assert.True(ownership.IsFollowing);
        Assert.False(ownership.ShouldShowBackToBottom);
    }

    [Fact]
    public void IncomingMessagesDoNotResumeFollowingWhileUserIsAway()
    {
        var ownership = new LiveChatScrollOwnership();
        ownership.ReportUserScroll(offset: 100, extent: 1000, viewport: 300);

        // 新消息让内容变高，但用户仍停在原处——不得把用户拉回底部。
        var shouldScroll = ownership.ReportAppend();

        Assert.False(shouldScroll);
        Assert.False(ownership.IsFollowing);
        Assert.True(ownership.ShouldShowBackToBottom);
    }

    [Fact]
    public void IncomingMessagesScrollOnlyWhileFollowing()
    {
        var ownership = new LiveChatScrollOwnership();

        Assert.True(ownership.ReportAppend());
        Assert.True(ownership.IsFollowing);
    }

    [Fact]
    public void RequestBackToBottom_ResumesFollowing()
    {
        var ownership = new LiveChatScrollOwnership();
        ownership.ReportUserScroll(offset: 0, extent: 1000, viewport: 300);

        ownership.RequestBackToBottom();

        Assert.True(ownership.IsFollowing);
        Assert.False(ownership.ShouldShowBackToBottom);
    }

    [Fact]
    public void ProgrammaticScrollsAreNotTreatedAsUserScroll()
    {
        var ownership = new LiveChatScrollOwnership();

        // 自动跟随过程中产生的中间位置不算"用户回看"。
        ownership.ReportProgrammaticScroll(offset: 200, extent: 1000, viewport: 300);

        Assert.True(ownership.IsFollowing);
        Assert.False(ownership.ShouldShowBackToBottom);
    }

    [Fact]
    public void Reset_RestoresFollowing()
    {
        var ownership = new LiveChatScrollOwnership();
        ownership.ReportUserScroll(offset: 0, extent: 1000, viewport: 300);

        ownership.Reset();

        Assert.True(ownership.IsFollowing);
        Assert.False(ownership.ShouldShowBackToBottom);
    }

    [Fact]
    public void NonPositiveExtent_IsTreatedAsBottom()
    {
        var ownership = new LiveChatScrollOwnership();

        Assert.False(ownership.ReportUserScroll(offset: 0, extent: 0, viewport: 0));
        Assert.True(ownership.IsFollowing);
    }
}
