using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Richasy.BiliKernel.Bili.Search;
using Richasy.WinUIKernel.Share.Toolkits;
using Richasy.WinUIKernel.Share.ViewModels;
using System.Reflection;

namespace BiliCopilot.UI.Tests;

[Collection("AvatarTransitionSession")]
public sealed class NavigationViewModelTests : IDisposable
{
    private readonly NavigationViewModel _model = new();
    private readonly MainFrame _main = new();
    private readonly OverlayFrame _overlay = new();
    private readonly NavigationView _view = new();
    private readonly MainWindow _window = new();
    private readonly SearchBoxViewModel _search = new(DispatchProxy.Create<ISearchService, NoNetwork>(), NullLogger<SearchBoxViewModel>.Instance);
    private readonly Settings _settings = new();

    public NavigationViewModelTests()
    {
        var app = new AppViewModel();
        app.Windows.Add(_window);
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new()
        {
            [typeof(AppViewModel)] = app,
            [typeof(SearchBoxViewModel)] = _search,
            [typeof(ISettingsToolkit)] = _settings,
            [typeof(NavigationViewModel)] = _model,
        };
    }

    [Fact]
    public void DefaultsAndInitializePreserveSettingsSelectionAndCollectionIdentity()
    {
        Assert.Null(_model.MenuItems);
        Assert.False(_model.IsOverlayOpen);
        Assert.False(_model.IsTopNavBarShown);
        Assert.False(_model.IsFavoritesPage);
        Assert.False(_model.IsHistoryPage);
        Assert.False(_model.IsViewLaterPage);
        Assert.False(_model.IsTemporaryOverlayClosed);
        var footer = _model.FooterItems;
        Assert.Empty(footer);
        NavigationTestServices.Settings.Value![SettingNames.LastSelectedFeaturePage] = typeof(MomentPage).FullName;
        var changes = new List<string?>();
        _model.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Initialize();
        Assert.True(_model.IsTopNavBarShown);
        Assert.NotNull(_model.MenuItems);
        Assert.Equal(typeof(MomentPage).FullName, Assert.Single(_model.MenuItems, item => item.IsSelected).PageKey);
        Assert.Equal(7, _model.MenuItems.Count);
        Assert.Equal(2, footer.Count);
        Assert.Same(footer, _model.FooterItems);
        Assert.Equal(new[] { "IsTopNavBarShown", "MenuItems" }, changes);
        var menu = _model.MenuItems;
        _model.Initialize(new(), new(), new());
        Assert.Same(menu, _model.MenuItems);
        Assert.Equal(2, footer.Count);
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void DynamicAndAppUpdateBadgesCanBeClearedIndependently()
    {
        Initialize();
        var dynamicItem = Assert.Single(_model.MenuItems, item => item.PageKey == typeof(MomentPage).FullName);
        var settingsItem = Assert.Single(_model.FooterItems, item => item.PageKey == typeof(SettingsPage).FullName);

        _model.SetMomentUnread(true);
        Assert.True(dynamicItem.ShowUnread);
        Assert.False(settingsItem.ShowUnread);

        _model.SetAppUpdateUnread(true);
        _model.SetMomentUnread(false);
        Assert.False(dynamicItem.ShowUnread);
        Assert.True(settingsItem.ShowUnread);

        _model.SetAppUpdateUnread(false);
        Assert.False(settingsItem.ShowUnread);
    }

    [Fact]
    public void InitializeFallsBackFromHiddenSavedPageAndHonorsHiddenTopBar()
    {
        _settings.HiddenPage = "IsMomentPageVisible";
        NavigationTestServices.Settings.Value![SettingNames.LastSelectedFeaturePage] = typeof(MomentPage).FullName;
        NavigationTestServices.Settings.Value![SettingNames.IsTopNavBarShown] = false;
        Initialize();
        Assert.False(_model.IsTopNavBarShown);
        Assert.Equal(typeof(PopularPage).FullName, Assert.Single(_model.MenuItems, item => item.IsSelected).PageKey);
        Assert.False(_model.MenuItems.Single(item => item.PageKey == typeof(MomentPage).FullName).IsVisible);
    }

    [Theory]
    [InlineData("IsOverlayOpen")]
    [InlineData("MenuItems")]
    [InlineData("IsTopNavBarShown")]
    [InlineData("IsFavoritesPage")]
    [InlineData("IsHistoryPage")]
    [InlineData("IsViewLaterPage")]
    public void PublicSetterNotifiesAfterChangeAndIgnoresEqualValue(string name)
    {
        var property = typeof(NavigationViewModel).GetProperty(name)!;
        var original = property.GetValue(_model);
        object replacement = name == "MenuItems" ? new List<AppNavigationItemViewModel>() : true;
        var events = new List<(string, object?)>();
        _model.PropertyChanging += (_, e) => { Assert.Equal(name, e.PropertyName); events.Add(("changing", property.GetValue(_model))); };
        _model.PropertyChanged += (_, e) =>
        {
            if (name == "IsOverlayOpen" && e.PropertyName == "CanGoBack")
            {
                return;
            }

            Assert.Equal(name, e.PropertyName);
            events.Add(("changed", property.GetValue(_model)));
        };
        property.SetValue(_model, replacement);
        property.SetValue(_model, replacement);
        Assert.Equal(new (string, object?)[] { ("changed", replacement) }, events);
        property.SetValue(_model, original);
        Assert.Equal(("changed", original), events[1]);
        Assert.Equal(2, events.Count);
    }

    [Theory]
    [InlineData(typeof(FavoritesPage), true, false, false)]
    [InlineData(typeof(HistoryPage), false, true, false)]
    [InlineData(typeof(ViewLaterPage), false, false, true)]
    public void OverlayNavigationUpdatesFlagsAndBackClearsThem(Type page, bool favorites, bool history, bool later)
    {
        Initialize();
        var parameter = new object();
        _search.Keyword = "old";
        _model.NavigateToOver(page, parameter);
        var content = Assert.IsAssignableFrom<NavigationTestPage>(_overlay.GetCurrentContent());
        Assert.Equal(page, content.GetType());
        Assert.Same(parameter, content.Parameter);
        Assert.True(_model.IsOverlayOpen);
        Assert.Equal(favorites, _model.IsFavoritesPage);
        Assert.Equal(history, _model.IsHistoryPage);
        Assert.Equal(later, _model.IsViewLaterPage);
        Assert.Equal(history ? "old" : string.Empty, _search.Keyword);
        Assert.Equal(1, _window.ActivationCount);
        _model.Back();
        Assert.Equal(1, content.CancelCount);
        Assert.Empty(_overlay.GetBackStack());
        Assert.False(_model.IsOverlayOpen);
        Assert.False(_model.IsFavoritesPage);
        Assert.False(_model.IsHistoryPage);
        Assert.False(_model.IsViewLaterPage);
        Assert.Equal(1, _main.FocusCount);
    }

    [Fact]
    public void MainNavigationTemporarilyClosesOverlayAndRepeatedPageCancelsLoading()
    {
        Initialize();
        INavServiceViewModel service = _model;
        var parameter = new object();
        service.NavigateTo(typeof(MomentPage), parameter);
        var main = Assert.IsType<MomentPage>(_main.GetCurrentContent());
        Assert.Same(parameter, main.Parameter);
        Assert.Same(_model.MenuItems.Single(item => item.PageKey == typeof(MomentPage).FullName), _view.SelectedItem);
        _model.NavigateToOver(typeof(FavoritesPage));
        var events = new List<string?>();
        _model.PropertyChanged += (_, e) => events.Add(e.PropertyName);
        service.NavigateTo(typeof(MomentPage), new object());
        Assert.Equal(new[] { "IsOverlayOpen", "CanGoBack", "IsTemporaryOverlayClosed", "IsFavoritesPage" }, events);
        Assert.Same(main, _main.GetCurrentContent());
        Assert.Same(parameter, main.Parameter);
        Assert.Equal(1, main.CancelCount);
        Assert.Single(_overlay.GetBackStack());
        Assert.True(_model.IsTemporaryOverlayClosed);
        _model.NavigateToOver(typeof(ViewLaterPage));
        Assert.Single(_overlay.GetBackStack());
        Assert.IsType<ViewLaterPage>(_overlay.GetCurrentContent());
        Assert.False(_model.IsTemporaryOverlayClosed);
        Assert.True(_model.IsViewLaterPage);
    }

    [Fact]
    public void SearchPreservesKeywordReusesPageAndClearCancelsCurrentPage()
    {
        Initialize();
        _search.Keyword = "selected";
        _model.Search("first");
        var page = Assert.IsType<SearchPage>(_overlay.GetCurrentContent());
        Assert.Equal("first", page.Parameter);
        Assert.Equal("selected", _search.Keyword);
        _model.IsOverlayOpen = false;
        _model.Search("second");
        Assert.Same(page, _overlay.GetCurrentContent());
        Assert.Equal(new[] { "second" }, page.ViewModel.Searches);
        Assert.True(_model.IsOverlayOpen);
        Assert.Equal(1, _window.ActivationCount);
        _model.TryClearOverlayPages();
        Assert.Equal(1, page.CancelCount);
        Assert.Empty(_overlay.GetBackStack());
        Assert.False(_model.IsOverlayOpen);
        Assert.False(_model.IsTemporaryOverlayClosed);
    }

    [Fact]
    public void UserSpaceNavigationSuppressesFadeOnlyForFreshMatchingTarget()
    {
        Initialize();
        var avatar = new Uri("https://i0.hdslb.com/bfs/face/avatar.jpg");
        var otherAvatar = new Uri("https://i0.hdslb.com/bfs/face/other.jpg");
        var userA = Profile("100", avatar);
        var userB = Profile("200", avatar);

        AvatarTransitionSession.Prepare("100", avatar, DateTimeOffset.Now);
        try
        {
            // 目标用户与头像都匹配且请求新鲜：抑制叠层页淡入。
            // 抑制时仍挂零时长显示动画，用来清掉隐藏动画留在页面上的 0 不透明度。
            _model.NavigateToOver(typeof(UserSpacePage), userA);
            var suppressed = Assert.IsType<UserSpacePage>(_overlay.GetCurrentContent()).ShowAnimations;
            Assert.NotNull(suppressed);
            var instant = Assert.IsType<CommunityToolkit.WinUI.Animations.OpacityAnimation>(Assert.Single(suppressed!));
            Assert.True(instant.Duration <= TimeSpan.FromMilliseconds(1));

            // 请求属于 A：导航到 B 不得抑制 B 的淡入。
            _model.NavigateToOver(typeof(UserSpacePage), userB);
            Assert.NotNull(Assert.IsType<UserSpacePage>(_overlay.GetCurrentContent()).ShowAnimations);

            // 同一用户但头像不同（例如右键菜单从其他数据源发起）也不抑制。
            _model.NavigateToOver(typeof(UserSpacePage), Profile("100", otherAvatar));
            Assert.NotNull(Assert.IsType<UserSpacePage>(_overlay.GetCurrentContent()).ShowAnimations);

            // 非用户空间页保持原有过渡。
            _model.NavigateToOver(typeof(FavoritesPage));
            Assert.NotNull(Assert.IsType<FavoritesPage>(_overlay.GetCurrentContent()).ShowAnimations);
        }
        finally
        {
            AvatarTransitionSession.Clear();
        }

        _model.NavigateToOver(typeof(UserSpacePage), userA);
        Assert.NotNull(Assert.IsType<UserSpacePage>(_overlay.GetCurrentContent()).ShowAnimations);
    }

    [Fact]
    public void ExpiredAvatarTransitionDoesNotSuppressFade()
    {
        Initialize();
        var avatar = new Uri("https://i0.hdslb.com/bfs/face/avatar.jpg");
        var userA = Profile("100", avatar);
        AvatarTransitionSession.Prepare(
            "100",
            avatar,
            DateTimeOffset.Now - AvatarTransitionPolicy.MaxPrepareAge - TimeSpan.FromSeconds(1));
        try
        {
            // 目标页始终没有出现时，过期请求不能永久抑制后续导航的淡入。
            _model.NavigateToOver(typeof(UserSpacePage), userA);
            Assert.NotNull(Assert.IsType<UserSpacePage>(_overlay.GetCurrentContent()).ShowAnimations);
        }
        finally
        {
            AvatarTransitionSession.Clear();
        }
    }

    [Fact]
    public void SettingsNavigationWithParameterUpdatesActiveSettingsPageDirectly()
    {
        Initialize();
        _model.NavigateTo(typeof(SettingsPage));
        var settings = Assert.IsType<SettingsPage>(_main.GetCurrentContent());
        var settingsItem = Assert.Single(_model.FooterItems, item => item.PageKey == typeof(SettingsPage).FullName);
        Assert.Same(settingsItem, _view.SelectedItem);
        Assert.Null(settings.Parameter);

        // 已在设置页时，带参数跳转到更新设置应直接转发参数而不触发 CancelLoading 回首页。
        _model.NavigateTo(typeof(SettingsPage), SettingsNavigationPolicy.UpdateNotificationRoute);
        Assert.Same(settings, _main.GetCurrentContent());
        Assert.Equal(SettingsNavigationPolicy.UpdateNotificationRoute, settings.Parameter);
        Assert.Equal(0, settings.CancelCount);

        // 叠层页打开在设置页之上时，同样关闭叠层并把更新路由送达设置页。
        _model.NavigateToOver(typeof(FavoritesPage));
        Assert.True(_model.IsOverlayOpen);
        _model.NavigateTo(typeof(SettingsPage), SettingsSection.Update);
        Assert.False(_model.IsOverlayOpen);
        Assert.Same(settings, _main.GetCurrentContent());
        Assert.Equal(SettingsSection.Update, settings.Parameter);
        Assert.Equal(0, settings.CancelCount);

        // 不带参数再次点击侧栏设置项时，仍走 CancelLoading 收回二级页。
        _model.NavigateTo(typeof(SettingsPage));
        Assert.Equal(1, settings.CancelCount);
    }

    [Fact]
    public void BackUsesTopBarToReturnFromSettingsSubpageWhenNoOverlayIsOpen()
    {
        Initialize();
        _model.NavigateTo(typeof(SettingsPage), SettingsNavigationPolicy.UpdateNotificationRoute);
        var settings = Assert.IsType<SettingsPage>(_main.GetCurrentContent());

        _model.IsSettingsSubpageOpen = true;
        Assert.True(_model.CanGoBack);

        _model.Back();
        Assert.Equal(1, settings.BackNavigationCount);

        // 叠层页打开时，顶栏返回先关闭叠层页，不误触设置页返回。
        _model.NavigateToOver(typeof(FavoritesPage));
        Assert.True(_model.CanGoBack);
        _model.Back();
        Assert.False(_model.IsOverlayOpen);
        Assert.True(_model.CanGoBack);
        Assert.Equal(1, settings.BackNavigationCount);
    }

    [Fact]
    public void NavigationBeforeInitializeRejectsWithoutActivatingWindow()
    {
        Assert.Throws<InvalidOperationException>(() => _model.NavigateTo(typeof(PopularPage)));
        Assert.Throws<InvalidOperationException>(() => _model.NavigateToOver(typeof(SearchPage)));
        Assert.Equal(0, _window.ActivationCount);
    }

    private void Initialize() => _model.Initialize(_view, _main, _overlay);

    private static Richasy.BiliKernel.Models.User.UserProfile Profile(string id, Uri avatar)
        => new(id, $"User {id}", new Richasy.BiliKernel.Models.Appearance.BiliImage(avatar));

    public void Dispose()
    {
        NavigationTestServices.Current.Value = null;
        NavigationTestServices.Settings.Value = null;
    }
    private sealed class Settings : ISettingsToolkit
    {
        public string? HiddenPage { get; set; }
        public T ReadLocalSetting<T>(string key, T fallback) => key == HiddenPage ? (T)(object)false : fallback;

        public void WriteLocalSetting<T>(string key, T value) { }
    }
    public class NoNetwork : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException("Navigation must not call the network.");
    }
}
