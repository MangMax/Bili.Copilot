// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Pages;
using BiliCopilot.UI.Pages.Overlay;

namespace BiliCopilot.UI.Controls;

/// <summary>
/// Creates the application's navigation pages without reflection. The external
/// INavServiceViewModel and AppNavigationItemViewModel carry an unannotated Type,
/// so constructor preservation cannot safely be propagated through that boundary.
/// Keep this catalog in sync with NavigationViewModel's menu and overlay callers.
/// </summary>
internal static class NavigationPageFactory
{
    public static UIElement Create(Type pageType)
    {
        if (pageType == typeof(PopularPage))
        {
            return new PopularPage();
        }

        if (pageType == typeof(MomentPage))
        {
            return new MomentPage();
        }

        if (pageType == typeof(VideoPartitionPage))
        {
            return new VideoPartitionPage();
        }

        if (pageType == typeof(LivePartitionPage))
        {
            return new LivePartitionPage();
        }

        if (pageType == typeof(AnimePage))
        {
            return new AnimePage();
        }

        if (pageType == typeof(CinemaPage))
        {
            return new CinemaPage();
        }

        if (pageType == typeof(ArticlePartitionPage))
        {
            return new ArticlePartitionPage();
        }

        if (pageType == typeof(MessagePage))
        {
            return new MessagePage();
        }

        if (pageType == typeof(SettingsPage))
        {
            return new SettingsPage();
        }

        if (pageType == typeof(ArticleReaderPage))
        {
            return new ArticleReaderPage();
        }

        if (pageType == typeof(FansPage))
        {
            return new FansPage();
        }

        if (pageType == typeof(FavoritesPage))
        {
            return new FavoritesPage();
        }

        if (pageType == typeof(FollowsPage))
        {
            return new FollowsPage();
        }

        if (pageType == typeof(HistoryPage))
        {
            return new HistoryPage();
        }

        if (pageType == typeof(SearchPage))
        {
            return new SearchPage();
        }

        if (pageType == typeof(UserSpacePage))
        {
            return new UserSpacePage();
        }

        if (pageType == typeof(ViewLaterPage))
        {
            return new ViewLaterPage();
        }

        throw new ArgumentException("The page type is not registered for application navigation.", nameof(pageType));
    }
}
