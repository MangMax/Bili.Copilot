// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// Shared article cover: 16:10 image, article badge and reading metrics.
/// </summary>
public sealed partial class ArticleMediaPresenter : ArticleMediaPresenterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleMediaPresenter"/> class.
    /// </summary>
    public ArticleMediaPresenter()
    {
        InitializeComponent();
    }
}

/// <summary>
/// Base type for the shared article cover presenter.
/// </summary>
public abstract class ArticleMediaPresenterBase : LayoutUserControlBase<ArticleItemViewModel>;
