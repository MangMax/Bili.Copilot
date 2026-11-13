// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 头像串联动画的资格判断与超时预算。
/// </summary>
/// <remarks>
/// 这些测试只断言外部可观察的决策：是否准备、是否启动、请求是否过期，
/// 不涉及 ConnectedAnimationService 的内部调用。
/// </remarks>
public class AvatarTransitionPolicyTests
{
    private static readonly Uri Avatar = new("https://i0.hdslb.com/bfs/face/avatar.jpg@104w_104h_1c.webp");
    private static readonly Uri OtherAvatar = new("https://i0.hdslb.com/bfs/face/other.jpg@104w_104h_1c.webp");
    private static readonly DateTimeOffset PreparedAt = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void CanPrepare_RequiresAnimationsVisibleAndNavigableSource(
        bool animationsEnabled,
        bool sourceVisible,
        bool canOpenUserSpace,
        bool expected)
        => Assert.Equal(
            expected,
            AvatarTransitionPolicy.CanPrepare(animationsEnabled, sourceVisible, canOpenUserSpace, "100", Avatar));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("100", true)]
    public void CanPrepare_RequiresUserIdentityAndAvatar(string? userId, bool avatarMissing)
        => Assert.False(AvatarTransitionPolicy.CanPrepare(
            true,
            true,
            true,
            userId,
            avatarMissing ? null : Avatar));

    [Fact]
    public void CanStart_AcceptsMatchingTargetWithinAgeBudget()
    {
        var request = new AvatarTransitionRequest("100", Avatar, PreparedAt);

        Assert.True(AvatarTransitionPolicy.CanStart(
            request, "100", Avatar, AvatarTransitionPolicy.MaxPrepareAge));
    }

    [Fact]
    public void CanStart_RejectsDifferentDestinationUser()
    {
        var request = new AvatarTransitionRequest("100", Avatar, PreparedAt);

        Assert.False(AvatarTransitionPolicy.CanStart(
            request, "200", Avatar, TimeSpan.Zero));
    }

    [Fact]
    public void CanStart_RejectsDifferentDestinationAvatar()
    {
        var request = new AvatarTransitionRequest("100", Avatar, PreparedAt);

        Assert.False(AvatarTransitionPolicy.CanStart(
            request, "100", OtherAvatar, TimeSpan.Zero));
    }

    [Fact]
    public void CanStart_RejectsStaleRequest()
    {
        var request = new AvatarTransitionRequest("100", Avatar, PreparedAt);

        Assert.False(AvatarTransitionPolicy.CanStart(
            request,
            "100",
            Avatar,
            AvatarTransitionPolicy.MaxPrepareAge + TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void CanStart_RejectsMissingRequestOrDestination()
    {
        Assert.False(AvatarTransitionPolicy.CanStart(null, "100", Avatar, TimeSpan.Zero));
        Assert.False(AvatarTransitionPolicy.CanStart(
            new AvatarTransitionRequest("100", Avatar, PreparedAt), "100", null, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(-1000, 250)]
    [InlineData(0, 250)]
    [InlineData(100, 150)]
    [InlineData(250, 0)]
    [InlineData(400, 0)]
    public void GetImageWaitBudget_LeavesOnlyTheRemainingImageBudget(int ageMs, int expectedMs)
    {
        var request = new AvatarTransitionRequest("100", Avatar, PreparedAt);

        var budget = AvatarTransitionPolicy.GetImageWaitBudget(
            request, TimeSpan.FromMilliseconds(ageMs));

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), budget);
    }

    [Fact]
    public void GetImageWaitBudget_WithoutRequestHasNoWait()
        => Assert.Equal(TimeSpan.Zero, AvatarTransitionPolicy.GetImageWaitBudget(null, TimeSpan.Zero));

    [Fact]
    public void Session_StoresSinglePendingRequestAndClearReleasesIt()
    {
        AvatarTransitionSession.Clear();

        AvatarTransitionSession.Prepare("100", Avatar, PreparedAt);
        Assert.True(AvatarTransitionSession.HasPending);
        Assert.Equal("100", AvatarTransitionSession.Current?.UserId);
        Assert.Equal(Avatar, AvatarTransitionSession.Current?.AvatarUri);
        Assert.Equal(PreparedAt, AvatarTransitionSession.Current?.PreparedAt);

        var later = PreparedAt.AddMilliseconds(10);
        AvatarTransitionSession.Prepare("200", OtherAvatar, later);
        Assert.Equal("200", AvatarTransitionSession.Current?.UserId);
        Assert.Equal(later, AvatarTransitionSession.Current?.PreparedAt);

        AvatarTransitionSession.Clear();
        Assert.False(AvatarTransitionSession.HasPending);
        Assert.Null(AvatarTransitionSession.Current);
    }
}
