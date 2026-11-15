// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.View;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Pages.Overlay;

/// <summary>
/// 合集详情页.
/// </summary>
public sealed partial class CollectionDetailPage : CollectionDetailPageBase, IParameterPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionDetailPage"/> class.
    /// </summary>
    public CollectionDetailPage() => InitializeComponent();

    /// <inheritdoc/>
    public void SetParameter(object? parameter)
    {
        if (parameter is CollectionDetailParameter detail)
        {
            ViewModel.Initialize(detail);
        }
    }
}

/// <summary>
/// 合集详情页基类.
/// </summary>
public abstract class CollectionDetailPageBase : LayoutPageBase<CollectionDetailPageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionDetailPageBase"/> class.
    /// </summary>
    protected CollectionDetailPageBase()
        => ViewModel = this.Get<CollectionDetailPageViewModel>();
}
