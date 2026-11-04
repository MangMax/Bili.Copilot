// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Controls.Article;

/// <summary>
/// 文章阅读器工具栏.
/// </summary>
public sealed partial class ArticleReaderToolBar : ArticleReaderPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleReaderToolBar"/> class.
    /// </summary>
    public ArticleReaderToolBar() => InitializeComponent();

    private static bool IsKnown(bool? state) => state.HasValue;

    private static string GetLikeHelp(bool? state)
        => ResourceToolkit.GetLocalizedString(state.HasValue ? StringNames.Like : StringNames.ArticleLikeStateUnknown);

    private static string GetFavoriteHelp(bool? state)
        => ResourceToolkit.GetLocalizedString(state.HasValue ? StringNames.Favorite : StringNames.ArticleFavoriteStateUnknown);
}
