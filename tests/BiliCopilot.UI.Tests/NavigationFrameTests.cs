using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Pages;
using BiliCopilot.UI.Pages.Overlay;

namespace BiliCopilot.UI.Tests;

public sealed class NavigationFrameTests
{
    [Theory]
    [InlineData(typeof(PopularPage))]
    [InlineData(typeof(MomentPage))]
    [InlineData(typeof(VideoPartitionPage))]
    [InlineData(typeof(LivePartitionPage))]
    [InlineData(typeof(AnimePage))]
    [InlineData(typeof(CinemaPage))]
    [InlineData(typeof(ArticlePartitionPage))]
    [InlineData(typeof(MessagePage))]
    [InlineData(typeof(SettingsPage))]
    [InlineData(typeof(ArticleReaderPage))]
    [InlineData(typeof(FansPage))]
    [InlineData(typeof(FavoritesPage))]
    [InlineData(typeof(FollowsPage))]
    [InlineData(typeof(HistoryPage))]
    [InlineData(typeof(SearchPage))]
    [InlineData(typeof(UserSpacePage))]
    [InlineData(typeof(PgcDetailPage))]
    [InlineData(typeof(ViewLaterPage))]
    public void FirstNavigationConstructsRequestedPageAndPassesParameter(Type pageType)
    {
        var parameter = new object();
        var main = new MainFrame();
        var overlay = new OverlayFrame();
        main.NavigateTo(pageType, parameter);
        overlay.NavigateTo(pageType, parameter);
        Assert.Equal(pageType, main.GetCurrentContent().GetType());
        Assert.Equal(pageType, overlay.GetCurrentContent()!.GetType());
        Assert.Same(parameter, ((NavigationTestPage)main.GetCurrentContent()).Parameter);
        Assert.Same(parameter, ((NavigationTestPage)overlay.GetCurrentContent()!).Parameter);
        Assert.NotSame(main.GetCurrentContent(), overlay.GetCurrentContent());
    }

    [Theory]
    [InlineData(typeof(object))]
    [InlineData(typeof(NavigationTestPage))]
    public void UnregisteredTypeIsRejectedWithoutChangingCurrentPages(Type unsupportedType)
    {
        var main = new MainFrame();
        var overlay = new OverlayFrame();
        main.NavigateTo(typeof(PopularPage));
        overlay.NavigateTo(typeof(SearchPage));
        var mainPage = main.GetCurrentContent();
        var overlayPage = overlay.GetCurrentContent();
        Assert.Throws<ArgumentException>(() => main.NavigateTo(unsupportedType));
        Assert.Throws<ArgumentException>(() => overlay.NavigateTo(unsupportedType));
        Assert.Same(mainPage, main.GetCurrentContent());
        Assert.Same(overlayPage, overlay.GetCurrentContent());
        Assert.Single(overlay.GetBackStack());
    }

    [Fact]
    public void ConstructorFailurePreservesWrapperAndBothFramesCurrentContent()
    {
        var main = new MainFrame();
        var overlay = new OverlayFrame();
        main.NavigateTo(typeof(MomentPage));
        overlay.NavigateTo(typeof(SearchPage));
        var mainPage = main.GetCurrentContent();
        var overlayPage = overlay.GetCurrentContent();
        var original = new InvalidOperationException("constructor sentinel");
        PopularPage.ConstructorFailure.Value = original;
        try
        {
            var mainError = Assert.Throws<System.Reflection.TargetInvocationException>(() => main.NavigateTo(typeof(PopularPage)));
            var overlayError = Assert.Throws<System.Reflection.TargetInvocationException>(() => overlay.NavigateTo(typeof(PopularPage)));
            Assert.Same(original, mainError.InnerException);
            Assert.Same(original, overlayError.InnerException);
            Assert.Same(mainPage, main.GetCurrentContent());
            Assert.Same(overlayPage, overlay.GetCurrentContent());
            Assert.Single(overlay.GetBackStack());
            Assert.Equal(Visibility.Visible, ((NavigationTestPage)mainPage).Visibility);
            Assert.Equal(Visibility.Visible, ((NavigationTestPage)overlayPage!).Visibility);
        }
        finally { PopularPage.ConstructorFailure.Value = null; }
        main.NavigateTo(typeof(PopularPage));
        overlay.NavigateTo(typeof(PopularPage));
        Assert.IsType<PopularPage>(main.GetCurrentContent());
        Assert.IsType<PopularPage>(overlay.GetCurrentContent());
        Assert.Equal(2, overlay.GetBackStack().Count());
    }
    [Fact]
    public void MainRevisitReusesPageUpdatesParameterInitializesAndCancelsHiddenPage()
    {
        var frame = new MainFrame();
        frame.NavigateTo(typeof(PopularPage), "first");
        var first = Assert.IsType<PopularPage>(frame.GetCurrentContent());
        Assert.Equal(0, first.InitializeCount);
        frame.NavigateTo(typeof(MomentPage));
        var second = Assert.IsType<MomentPage>(frame.GetCurrentContent());
        Assert.Equal(Visibility.Collapsed, first.Visibility);
        Assert.Equal(1, first.CancelCount);
        frame.NavigateTo(typeof(PopularPage), "next");
        Assert.Same(first, frame.GetCurrentContent());
        Assert.Equal("next", first.Parameter);
        Assert.Equal(1, first.InitializeCount);
        Assert.Equal(Visibility.Visible, first.Visibility);
        Assert.Equal(Visibility.Collapsed, second.Visibility);
        Assert.Equal(1, second.CancelCount);
    }

    [Fact]
    public void OverlayRevisitMovesCachedPageToTopAndBackRevealsPreviousPage()
    {
        var frame = new OverlayFrame();
        frame.NavigateTo(typeof(SearchPage), "first");
        var search = Assert.IsType<SearchPage>(frame.GetCurrentContent());
        frame.NavigateTo(typeof(UserSpacePage));
        var user = Assert.IsType<UserSpacePage>(frame.GetCurrentContent());
        frame.NavigateTo(typeof(SearchPage), "next");
        Assert.Equal(new object[] { user, search }, frame.GetBackStack());
        Assert.Same(search, frame.GetCurrentContent());
        Assert.Equal("next", search.Parameter);
        Assert.Equal(0, search.InitializeCount);
        Assert.Equal(1, search.CancelCount);
        Assert.Equal(1, user.CancelCount);
        Assert.Equal(Visibility.Collapsed, user.Visibility);
        frame.GoBack();
        Assert.Same(user, frame.GetCurrentContent());
        Assert.Equal(Visibility.Visible, user.Visibility);
        frame.GoBack();
        frame.GoBack();
        Assert.Empty(frame.GetBackStack());
        Assert.Null(frame.GetCurrentContent());
    }

    [Fact]
    public void SuppressedFadeStillRestoresOpacityWithInstantShowAnimation()
    {
        var frame = new OverlayFrame();
        frame.NavigateTo(typeof(UserSpacePage), animateIn: false);
        var space = Assert.IsType<UserSpacePage>(frame.GetCurrentContent());

        // 隐藏时页面会停在上一次隐藏动画的 0 不透明度；抑制淡入不能把显示动画设为 null，
        // 否则复用的页面整页透明（元素都在，画面是空的）。
        Assert.NotNull(space.ShowAnimations);
        var show = Assert.Single(space.ShowAnimations!);
        Assert.Equal(1d, show.To);
        Assert.True(show.Duration <= TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void BackedParameterPageStaysInTreeAndIsReused()
    {
        var frame = new OverlayFrame();
        frame.NavigateTo(typeof(SearchPage), "first");
        var search = Assert.IsType<SearchPage>(frame.GetCurrentContent());
        frame.GoBack();
        Assert.Empty(frame.GetBackStack());
        Assert.Null(frame.GetCurrentContent());

        // 返回后的页面留在视觉树里，再次进入同一页面复用同一实例，
        // 省掉重建与重新挂载的开销（头像串联动画依赖这一点）。
        frame.NavigateTo(typeof(SearchPage), "second");
        Assert.Same(search, frame.GetCurrentContent());
        Assert.Equal("second", search.Parameter);
        Assert.Equal(Visibility.Visible, search.Visibility);
    }

    [Fact]
    public void RetainedPagesKeepOnlyTheMostRecentBackNavigations()
    {
        var frame = new OverlayFrame();
        frame.NavigateTo(typeof(SearchPage));
        var search = Assert.IsType<SearchPage>(frame.GetCurrentContent());
        frame.NavigateTo(typeof(UserSpacePage));
        frame.NavigateTo(typeof(ArticleReaderPage));
        var article = Assert.IsType<ArticleReaderPage>(frame.GetCurrentContent());

        frame.GoBack();
        frame.GoBack();
        frame.GoBack();

        // 只保留最近两次返回的页面，更早的搜索页被真正移除。
        frame.NavigateTo(typeof(ArticleReaderPage));
        Assert.NotSame(article, frame.GetCurrentContent());
        frame.GoBack();
        frame.NavigateTo(typeof(SearchPage));
        Assert.Same(search, frame.GetCurrentContent());
    }

    [Fact]
    public void OverlayNavigationSuppressesAndRestoresFadeOnNewAndReusedPages()
    {
        var frame = new OverlayFrame();
        frame.NavigateTo(typeof(SearchPage), animateIn: false);
        var search = Assert.IsType<SearchPage>(frame.GetCurrentContent());
        Assert.True(IsInstantShow(search));
        Assert.True(IsFadingShow(search) is false);
        Assert.NotNull(search.HideAnimations);

        frame.NavigateTo(typeof(UserSpacePage), animateIn: false);
        var space = Assert.IsType<UserSpacePage>(frame.GetCurrentContent());
        Assert.True(IsInstantShow(space));

        frame.NavigateTo(typeof(SearchPage));
        Assert.Same(search, frame.GetCurrentContent());
        Assert.True(IsFadingShow(search));

        frame.NavigateTo(typeof(UserSpacePage), animateIn: false);
        Assert.Same(space, frame.GetCurrentContent());
        Assert.True(IsInstantShow(space));

        frame.NavigateTo(typeof(SearchPage));
        Assert.Same(search, frame.GetCurrentContent());
        Assert.True(IsFadingShow(search));
    }

    private static bool IsInstantShow(NavigationTestPage page)
        => page.ShowAnimations is { Count: 1 } set
            && set[0].To == 1
            && set[0].Duration <= TimeSpan.FromMilliseconds(1);

    private static bool IsFadingShow(NavigationTestPage page)
        => page.ShowAnimations is { Count: 1 } set
            && set[0].To == 1
            && set[0].Duration >= TimeSpan.FromMilliseconds(100);

    [Fact]
    public void ClearedOverlayConstructsFreshPageAndNullNavigationLeavesContentUnchanged()
    {
        var frame = new OverlayFrame();
        frame.NavigateTo(typeof(SearchPage));
        var original = frame.GetCurrentContent();
        frame.NavigateTo(null!);
        Assert.Same(original, frame.GetCurrentContent());
        frame.ClearBackStack();
        Assert.Null(frame.GetCurrentContent());
        frame.NavigateTo(typeof(SearchPage));
        Assert.NotSame(original, frame.GetCurrentContent());
        var main = new MainFrame();
        main.NavigateTo(null!);
        Assert.Null(main.GetCurrentContent());
        main.NavigateTo(typeof(PopularPage));
        var first = main.GetCurrentContent();
        main.NavigateTo(null!);
        Assert.Same(first, main.GetCurrentContent());
    }
}
