// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Accounts;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.Controls;

/// <summary>
/// 根布局，用于包裹整个应用程序的布局.
/// </summary>
public sealed partial class RootLayout : RootLayoutBase
{
    /// <summary>
    /// 应用视图模型，XAML 中绑定初始加载状态.
    /// </summary>
    private readonly AppViewModel _appViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="RootLayout"/> class.
    /// </summary>
    public RootLayout()
    {
        _appViewModel = this.Get<AppViewModel>();
        InitializeComponent();
        var owner = AccountSession.Find(this);
        owner?.Bind(MainFrame);
        owner?.Bind(OverlayFrame);
    }

    /// <summary>
    /// 获取主标题栏.
    /// </summary>
    /// <returns><see cref="AppTitleBar"/>.</returns>
    public AppTitleBar GetMainTitleBar() => MainTitleBar;

    public void TryFocusSearchBox()
        => SearchBox.Focus(FocusState.Programmatic);

    internal (Type Page, string? Parameter)? RestoreAccountRoute { get; set; }

    internal (Type Page, string? Parameter)? GetAccountRoute() => OverlayFrame.GetCurrentContent() switch
    {
        FavoritesPage => (typeof(FavoritesPage), null),
        ViewLaterPage => (typeof(ViewLaterPage), null),
        HistoryPage => (typeof(HistoryPage), null),
        PgcDetailPage { SeasonId: { } seasonId } => (typeof(PgcDetailPage), seasonId),
        _ => null,
    };

    internal void ReleaseAccountPages()
    {
        ViewModel.TryClearOverlayPages();
        MainFrame.ReleaseAccountPages();
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        ViewModel.Initialize(NavView, MainFrame, OverlayFrame);
        if (this.Get<AccountViewModel>() is { MyProfile: not null } account)
        {
            account.UpdateUnreadCommand.Execute(default);
        }
        ViewModel.SetMomentUnread(this.Get<MomentPageViewModel>().HasNewMoments);
        ViewModel.SetAppUpdateUnread(this.Get<AppUpdateViewModel>().HasAvailableUpdate);
        var selectedItem = ViewModel.MenuItems.FirstOrDefault(p => p.IsSelected);
        if (selectedItem is not null)
        {
            NavView.SelectedItem = selectedItem;
            selectedItem.NavigateCommand.Execute(default);
        }

        if (RestoreAccountRoute is { } route)
        {
            RestoreAccountRoute = null;
            ViewModel.NavigateToOver(route.Page, route.Parameter);
        }
    }

    private void OnNavViewItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        _ = this;
        var item = args.InvokedItemContainer as AppNavigationViewItem;
        var context = item?.Tag as AppNavigationItemViewModel;
        context?.NavigateCommand.Execute(default);
    }

    private void OnBackRequested(object sender, EventArgs e)
        => ViewModel.Back();

    private void OnFavoriteButtonClick(object sender, EventArgs e)
        => ViewModel.NavigateToOver(typeof(FavoritesPage), default);

    private void OnViewLaterButtonClick(object sender, EventArgs e)
        => ViewModel.NavigateToOver(typeof(ViewLaterPage), default);

    private void OnHistoryButtonClick(object sender, EventArgs e)
        => ViewModel.NavigateToOver(typeof(HistoryPage), default);
}

/// <summary>
/// 根布局基类.
/// </summary>
public abstract class RootLayoutBase : LayoutUserControlBase<NavigationViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RootLayoutBase"/> class.
    /// </summary>
    protected RootLayoutBase() => ViewModel = this.Get<NavigationViewModel>();
}
