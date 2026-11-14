// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using Richasy.WinUIKernel.Share.Base;
using System.ComponentModel;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// PGC 播放器页面剧集控件.
/// </summary>
public sealed partial class PgcEpisodesSection : PgcEpisodesSectionBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcEpisodesSection"/> class.
    /// </summary>
    public PgcEpisodesSection() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        InitializeLayoutAsync();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PgcPlayerEpisodeSectionDetailViewModel.Episodes) or nameof(PgcPlayerEpisodeSectionDetailViewModel.SelectedGroup))
        {
            // 切换分组后重新定位到正在播放的那一集.
            InitializeLayoutAsync();
        }
    }

    private async void OnIndexToggledAsync(object sender, RoutedEventArgs e)
    {
        await Task.Delay(100);
        InitializeLayoutAsync();
    }

    private async void InitializeLayoutAsync()
    {
        await Task.Delay(100);
        if (ViewModel.Episodes is not { } episodes || XamlRoot is null)
        {
            return;
        }

        var selectedItem = episodes.Find(p => p.IsSelected);
        if (selectedItem is null)
        {
            return;
        }

        var index = episodes.IndexOf(selectedItem);
        if (index < 0)
        {
            return;
        }

        if (ViewModel.ShowIndexLayout)
        {
            if (IndexView is null)
            {
                return;
            }

            var offset = (40 * (index / 7)) - IndexView.ViewportHeight;
            if (offset > 0)
            {
                IndexView.ScrollTo(0, offset);
            }
        }
        else
        {
            if (DefaultView is null)
            {
                return;
            }

            var offset = (86 * index) - DefaultView.ViewportHeight;
            if (offset > 0)
            {
                DefaultView.ScrollTo(0, offset + (DefaultView.ViewportHeight / 2));
            }
        }
    }
}

/// <summary>
/// PGC 播放器页面剧集控件基类.
/// </summary>
public abstract class PgcEpisodesSectionBase : LayoutUserControlBase<PgcPlayerEpisodeSectionDetailViewModel>
{
}
