using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

public sealed class PageTransitionPolicyTests
{
    [Fact]
    public void FirstBoundValueDoesNotAnimate()
    {
        Assert.False(PageTransitionPolicy.ShouldAnimateTrigger(
            isLoaded: true,
            animationsEnabled: true,
            hasPreviousValue: false,
            previousValue: null,
            currentValue: new object()));
    }

    [Fact]
    public void RepeatedValueDoesNotAnimate()
    {
        var value = new object();
        Assert.False(PageTransitionPolicy.ShouldAnimateTrigger(
            isLoaded: true,
            animationsEnabled: true,
            hasPreviousValue: true,
            previousValue: value,
            currentValue: value));
    }

    [Theory]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, true, true)]
    [InlineData(true, true, true, false, false)]
    public void TriggerAnimationRequiresLoadedEnabledPreviousAndNewValue(
        bool isLoaded,
        bool animationsEnabled,
        bool hasPreviousValue,
        bool hasCurrentValue,
        bool expected)
    {
        var previous = new object();
        var current = hasCurrentValue ? new object() : null;
        Assert.Equal(expected, PageTransitionPolicy.ShouldAnimateTrigger(
            isLoaded,
            animationsEnabled,
            hasPreviousValue,
            previous,
            current));
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, true, false)]
    public void ShowAnimationRequiresLoadedEnabledAndHiddenBeforehand(
        bool isLoaded,
        bool animationsEnabled,
        bool wasVisible,
        bool expected)
    {
        Assert.Equal(expected, PageTransitionPolicy.ShouldAnimateShow(isLoaded, animationsEnabled, wasVisible));
    }

    [Fact]
    public void TabsSlideWithTheirOrder()
    {
        Assert.Equal(
            PageTransitionDirection.FromRight,
            PageTransitionPolicy.GetStepDirection(PageTransitionKind.Tab, 0, 1));
        Assert.Equal(
            PageTransitionDirection.FromLeft,
            PageTransitionPolicy.GetStepDirection(PageTransitionKind.Tab, 2, 1));
        Assert.Equal(
            PageTransitionDirection.FromRight,
            PageTransitionPolicy.GetStepDirection(PageTransitionKind.Tab, -1, 1));
    }

    [Fact]
    public void SidebarsFollowTheSelectionMovement()
    {
        // 选中项往下移动时内容从上往下落，往上移动时从下往上升.
        Assert.Equal(
            PageTransitionDirection.FromTop,
            PageTransitionPolicy.GetStepDirection(PageTransitionKind.Sidebar, 0, 2));
        Assert.Equal(
            PageTransitionDirection.FromBottom,
            PageTransitionPolicy.GetStepDirection(PageTransitionKind.Sidebar, 2, 0));
        Assert.Equal(
            PageTransitionDirection.FromBottom,
            PageTransitionPolicy.GetStepDirection(PageTransitionKind.Sidebar, 1, 1));
    }

    [Fact]
    public void EachSceneUsesItsOwnMotion()
    {
        // 页面统一上浮，不跟随选择方向.
        Assert.Equal((0f, PageTransitionPolicy.PageOffset), PageTransitionPolicy.GetOffset(PageTransitionKind.Page, PageTransitionDirection.FromRight));
        Assert.Equal((0f, PageTransitionPolicy.PageOffset), PageTransitionPolicy.GetOffset(PageTransitionKind.Page, PageTransitionDirection.FromTop));

        // 标签只横向，侧栏只纵向.
        Assert.Equal((-PageTransitionPolicy.TabOffset, 0f), PageTransitionPolicy.GetOffset(PageTransitionKind.Tab, PageTransitionDirection.FromLeft));
        Assert.Equal((PageTransitionPolicy.TabOffset, 0f), PageTransitionPolicy.GetOffset(PageTransitionKind.Tab, PageTransitionDirection.FromRight));
        Assert.Equal((0f, PageTransitionPolicy.SidebarOffset), PageTransitionPolicy.GetOffset(PageTransitionKind.Sidebar, PageTransitionDirection.FromBottom));
        Assert.Equal((0f, -PageTransitionPolicy.SidebarOffset), PageTransitionPolicy.GetOffset(PageTransitionKind.Sidebar, PageTransitionDirection.FromTop));
    }

    [Fact]
    public void PageSceneIsMoreVisibleThanTabAndSidebar()
    {
        Assert.True(PageTransitionPolicy.GetStartOpacity(PageTransitionKind.Page)
            < PageTransitionPolicy.GetStartOpacity(PageTransitionKind.Tab));
        Assert.True(PageTransitionPolicy.GetFadeDuration(PageTransitionKind.Page)
            >= PageTransitionPolicy.GetFadeDuration(PageTransitionKind.Tab));
        Assert.True(PageTransitionPolicy.GetSlideDuration(PageTransitionKind.Page)
            >= PageTransitionPolicy.GetSlideDuration(PageTransitionKind.Tab));
    }

    [Fact]
    public void AutoHostAcceptsAnySceneButTypedHostOnlyItsOwn()
    {
        Assert.True(PageTransitionPolicy.AcceptsPending(PageTransitionKind.Auto, PageTransitionKind.Tab));
        Assert.True(PageTransitionPolicy.AcceptsPending(PageTransitionKind.Auto, PageTransitionKind.Sidebar));
        Assert.True(PageTransitionPolicy.AcceptsPending(PageTransitionKind.Tab, PageTransitionKind.Tab));
        Assert.False(PageTransitionPolicy.AcceptsPending(PageTransitionKind.Tab, PageTransitionKind.Sidebar));
        Assert.False(PageTransitionPolicy.AcceptsPending(PageTransitionKind.Sidebar, PageTransitionKind.Tab));
    }

    [Fact]
    public void SessionPublishesDirectionForChangedSelectionOnly()
    {
        var now = DateTimeOffset.Now;
        PageTransitionSession.Clear();
        PageTransitionSession.Publish(PageTransitionKind.Tab, 0, 1, now);
        var request = PageTransitionSession.CurrentAt(now);
        Assert.NotNull(request);
        Assert.Equal(PageTransitionKind.Tab, request!.Kind);
        Assert.Equal(PageTransitionDirection.FromRight, request.Direction);

        PageTransitionSession.Publish(PageTransitionKind.Sidebar, 2, 2, now);
        request = PageTransitionSession.CurrentAt(now);
        Assert.NotNull(request);
        Assert.Equal(PageTransitionKind.Tab, request!.Kind);

        PageTransitionSession.Publish(PageTransitionKind.Sidebar, 3, 1, now);
        request = PageTransitionSession.CurrentAt(now);
        Assert.Equal(PageTransitionKind.Sidebar, request!.Kind);
        Assert.Equal(PageTransitionDirection.FromBottom, request.Direction);
        PageTransitionSession.Clear();
    }

    [Fact]
    public void ExpiredOrFutureSessionDirectionsAreReleased()
    {
        var now = DateTimeOffset.Now;
        PageTransitionSession.Clear();

        // 过期读取会释放请求，之后的读取不再返回它.
        PageTransitionSession.Publish(PageTransitionKind.Tab, 0, 1, now);
        Assert.Null(PageTransitionSession.CurrentAt(now + PageTransitionPolicy.RequestLifetime + TimeSpan.FromMilliseconds(1)));
        Assert.Null(PageTransitionSession.CurrentAt(now));

        // 读取时刻早于发布时刻同样视为无效.
        PageTransitionSession.Publish(PageTransitionKind.Tab, 0, 1, now);
        Assert.Null(PageTransitionSession.CurrentAt(now - TimeSpan.FromSeconds(5)));

        // 有效期内仍可读取.
        PageTransitionSession.Publish(PageTransitionKind.Tab, 0, 1, now);
        Assert.NotNull(PageTransitionSession.CurrentAt(now));
        PageTransitionSession.Clear();
    }

    [Fact]
    public void IndexOfMatchesReferenceAndValue()
    {
        var first = new object();
        var items = new List<object> { first, "second", 3 };
        Assert.Equal(0, PageTransitionPolicy.IndexOf(items, first));
        Assert.Equal(1, PageTransitionPolicy.IndexOf(items, "second"));
        Assert.Equal(-1, PageTransitionPolicy.IndexOf(items, new object()));
        Assert.Equal(-1, PageTransitionPolicy.IndexOf(null, first));
    }
}
