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
[Collection("AvatarTransitionSession")]
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
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("100", false)]
    public void CanPrepare_RequiresUserIdentityAndAvatar(string? userId, bool avatarPresent)
        => Assert.False(AvatarTransitionPolicy.CanPrepare(
            true,
            true,
            true,
            userId,
            avatarPresent ? Avatar : null));

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
    [InlineData("100", true, true)]
    [InlineData("200", true, false)]
    [InlineData("100", false, false)]
    public void Matches_RequiresSameUserAndSameAvatar(
        string destinationUserId,
        bool sameAvatar,
        bool expected)
    {
        var request = new AvatarTransitionRequest("100", Avatar, PreparedAt);

        Assert.Equal(
            expected,
            AvatarTransitionPolicy.Matches(
                request,
                destinationUserId,
                sameAvatar ? Avatar : OtherAvatar));
    }

    [Fact]
    public void Matches_WithoutRequestNeverMatches()
        => Assert.False(AvatarTransitionPolicy.Matches(null, "100", Avatar));

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void IsFresh_UsesMaxPrepareAgeIncludingBoundary(int ageMs, bool expected)
    {
        var request = new AvatarTransitionRequest("100", Avatar, PreparedAt);

        Assert.Equal(
            expected,
            AvatarTransitionPolicy.IsFresh(request, PreparedAt.AddMilliseconds(ageMs)));
    }

    [Fact]
    public void CanStartWithNowOnlyAcceptsMatchingFreshRequest()
    {
        var request = new AvatarTransitionRequest("100", Avatar, PreparedAt);

        Assert.True(AvatarTransitionPolicy.CanStart(
            request, "100", Avatar, PreparedAt.AddMilliseconds(10)));
        Assert.False(AvatarTransitionPolicy.CanStart(
            request, "200", Avatar, PreparedAt.AddMilliseconds(10)));
        Assert.False(AvatarTransitionPolicy.CanStart(
            request, "100", Avatar,
            PreparedAt + AvatarTransitionPolicy.MaxPrepareAge + TimeSpan.FromMilliseconds(1)));
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
        var first = DateTimeOffset.Now;

        AvatarTransitionSession.Prepare("100", Avatar, first);
        var stored = AvatarTransitionSession.CurrentAt(first);
        Assert.Equal("100", stored?.UserId);
        Assert.Equal(Avatar, stored?.AvatarUri);
        Assert.Equal(first, stored?.PreparedAt);

        var later = first.AddMilliseconds(10);
        AvatarTransitionSession.Prepare("200", OtherAvatar, later);
        Assert.Equal("200", AvatarTransitionSession.CurrentAt(later)?.UserId);
        Assert.Equal(later, AvatarTransitionSession.CurrentAt(later)?.PreparedAt);

        AvatarTransitionSession.Clear();
        Assert.Null(AvatarTransitionSession.Current);
    }

    [Fact]
    public void Session_ExpiresAndReleasesRequestAfterMaxPrepareAge()
    {
        AvatarTransitionSession.Clear();
        var preparedAt = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        AvatarTransitionSession.Prepare("100", Avatar, preparedAt);

        var lastFresh = preparedAt + AvatarTransitionPolicy.MaxPrepareAge;
        Assert.NotNull(AvatarTransitionSession.CurrentAt(lastFresh));

        var expired = AvatarTransitionSession.CurrentAt(lastFresh.AddMilliseconds(1));
        Assert.Null(expired);

        // 过期读取会自行释放请求：回到准备时刻也不能再读到它。
        Assert.Null(AvatarTransitionSession.CurrentAt(preparedAt));
    }
}
