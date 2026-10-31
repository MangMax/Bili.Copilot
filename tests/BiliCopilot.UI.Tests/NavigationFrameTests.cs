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
