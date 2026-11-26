// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Services.Search;

internal interface ISearchLinkNavigator
{
    Task OpenAsync(BiliSearchLink link);

    void ShowFailure();
}
