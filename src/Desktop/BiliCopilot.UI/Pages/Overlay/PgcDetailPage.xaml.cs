// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.UI.Xaml.Media;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Pages.Overlay;

/// <summary>
/// 番剧/电影/电视剧详情页.
/// </summary>
public sealed partial class PgcDetailPage : PgcDetailPageBase, IParameterPage, ICancelPageViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcDetailPage"/> class.
    /// </summary>
    public PgcDetailPage() => InitializeComponent();

    /// <inheritdoc/>
    public void SetParameter(object? parameter)
    {
        if (parameter is string seasonId)
        {
            // 叠层页按类型缓存，复用时只换参数.
            ViewModel.InitializeCommand.Execute(seasonId);
        }
    }

    /// <inheritdoc/>
    public void CancelLoading() => ViewModel.CancelLoad();

    /// <inheritdoc/>
    protected override void OnPageUnloaded() => CancelLoading();

    private void OnEpisodeClick(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: EpisodeItemViewModel episode })
        {
            ViewModel.PlayEpisodeCommand.Execute(episode);
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Backdrop.Width = e.NewSize.Width;
        PageLayout.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
    }
}

/// <summary>
/// 番剧详情页基类.
/// </summary>
public abstract class PgcDetailPageBase : LayoutPageBase<PgcDetailPageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcDetailPageBase"/> class.
    /// </summary>
    protected PgcDetailPageBase()
    {
        ViewModel = this.Get<PgcDetailPageViewModel>();
    }
}
