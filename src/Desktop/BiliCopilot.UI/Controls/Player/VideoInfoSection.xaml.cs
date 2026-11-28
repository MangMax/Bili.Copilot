// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Richasy.BiliKernel.Models.Base;
using Richasy.BiliKernel.Models.Search;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// 视频信息区域.
/// </summary>
public sealed partial class VideoInfoSection : VideoPlayerControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VideoInfoSection"/> class.
    /// </summary>
    public VideoInfoSection() => InitializeComponent();

    private void OnTagButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BiliTag tag })
        {
            this.Get<SearchBoxViewModel>().SearchCommand.Execute(new SearchSuggestItemViewModel(new SearchSuggestItem(tag.Name, tag.Name)));
        }
    }
}
