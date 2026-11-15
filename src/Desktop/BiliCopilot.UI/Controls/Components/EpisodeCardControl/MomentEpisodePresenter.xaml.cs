// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Media;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// Moment episode presenter.
/// </summary>
public sealed partial class MomentEpisodePresenter : EpisodeCardPresenter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MomentEpisodePresenter"/> class.
    /// </summary>
    public MomentEpisodePresenter() => InitializeComponent();

    private void OnContentClick(object sender, RoutedEventArgs args)
    {
        // 番剧动态没有视频卡那样独立的播放按钮，点击媒体区域交给动态条目自己处理：
        // 有 ep 号时进播放器，只有 ss 号的“出差番剧”回退到网页。
        var moment = FindParent<MomentCardControl>();
        if (moment?.ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.ActivateCommand.Execute(default);
    }

    private T? FindParent<T>()
        where T : DependencyObject
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
