// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Services.Search;

internal sealed class SearchLinkNavigator(NavigationViewModel navigation, AppViewModel app) : ISearchLinkNavigator
{
    public async Task OpenAsync(BiliSearchLink link)
    {
        if (link.Kind == BiliSearchLinkKind.Article)
        {
            var article = new ArticleIdentifier(link.Id, default, default, default);
            navigation.NavigateToOver(typeof(ArticleReaderPage), article);
            return;
        }

        var identifier = new MediaIdentifier(link.Id, null, null);
        var snapshot = link.Kind switch
        {
            BiliSearchLinkKind.Video => new MediaSnapshot(new VideoInformation(identifier, default)),
            BiliSearchLinkKind.Live => new MediaSnapshot(new LiveInformation(identifier)),
            BiliSearchLinkKind.Episode => new MediaSnapshot(null, new EpisodeInformation(identifier, default)),
            BiliSearchLinkKind.Season => new MediaSnapshot(new SeasonInformation(identifier), null),
            _ => throw new ArgumentOutOfRangeException(nameof(link)),
        };

        await app.OpenPlayerCommand.ExecuteAsync(snapshot);
    }

    public void ShowFailure()
        => app.ShowTipCommand.Execute(
            (ResourceToolkit.GetLocalizedString(StringNames.SearchLinkOpenFailed), InfoType.Error));
}
