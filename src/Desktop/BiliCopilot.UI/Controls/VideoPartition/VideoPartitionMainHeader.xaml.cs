// Copyright (c) Bili Copilot. All rights reserved.

using System.ComponentModel;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Richasy.BiliKernel.Models;

namespace BiliCopilot.UI.Controls.VideoPartition;

/// <summary>
/// 视频分区主区域头部.
/// </summary>
public sealed partial class VideoPartitionMainHeader : VideoPartitionDetailControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VideoPartitionMainHeader"/> class.
    /// </summary>
    public VideoPartitionMainHeader() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        Selector.SelectionChanged += OnSelectorChanged;
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        InitializeChildPartitions();
        ApplyUndoRefreshVisibility(animate: false);
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.Initialized -= OnViewModelInitialized;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        Selector.SelectionChanged -= OnSelectorChanged;
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(VideoPartitionDetailViewModel? oldValue, VideoPartitionDetailViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Initialized -= OnViewModelInitialized;
            oldValue.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (newValue is null)
        {
            return;
        }

        newValue.Initialized += OnViewModelInitialized;
        newValue.PropertyChanged += OnViewModelPropertyChanged;
        InitializeChildPartitions();
        ApplyUndoRefreshVisibility(animate: false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoPartitionDetailViewModel.CanUndoRefresh))
        {
            ApplyUndoRefreshVisibility(animate: true);
        }
    }

    private void ApplyUndoRefreshVisibility(bool animate)
    {
        if (ViewModel is null)
        {
            return;
        }

        UndoRefreshButtonAnimator.Apply(
            UndoRefreshButton,
            ViewModel.CanUndoRefresh,
            animate,
            () => !ViewModel.CanUndoRefresh);
    }

    private void OnViewModelInitialized(object? sender, EventArgs e)
        => InitializeChildPartitions();

    private void OnSortTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Any()
            && e.AddedItems[0] is PartitionVideoSortType sortType
            && sortType != ViewModel.SelectedSortType)
        {
            ViewModel.ChangeSortTypeCommand.Execute(sortType);
        }
    }

    private void OnSelectorChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is null)
        {
            return;
        }

        var item = sender.SelectedItem.Tag as PartitionViewModel;
        if (item is not null && item != ViewModel.CurrentPartition)
        {
            ViewModel.ChangeChildPartitionCommand.Execute(item);
        }
    }

    private void InitializeChildPartitions()
    {
        Selector.Items.Clear();
        if (ViewModel.Children is not null)
        {
            foreach (var item in ViewModel.Children)
            {
                Selector.Items.Add(new SelectorBarItem()
                {
                    Text = item.Title ?? default,
                    Tag = item,
                });
            }
        }

        Selector.SelectedItem = ViewModel.IsRecommend
            ? Selector.Items.FirstOrDefault()
            : Selector.Items.FirstOrDefault(p => p.Tag == ViewModel.CurrentPartition);
    }
}
