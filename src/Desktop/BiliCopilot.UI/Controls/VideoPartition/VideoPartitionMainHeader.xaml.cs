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
    private VideoPartitionDetailViewModel? _subscribedViewModel;

    public VideoPartitionMainHeader()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    protected override void OnViewModelChanged(VideoPartitionDetailViewModel? oldValue, VideoPartitionDetailViewModel? newValue)
    {
        Detach();
        if (IsLoaded) Attach();
    }

    private void Attach()
    {
        Detach();
        _subscribedViewModel = ViewModel;
        Selector.SelectionChanged += OnSelectorChanged;
        if (_subscribedViewModel is null) return;
        _subscribedViewModel.Initialized += OnViewModelInitialized;
        _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        InitializeChildPartitions();
        ApplyUndoRefreshVisibility(animate: false);
    }

    private void Detach()
    {
        if (Selector is not null) Selector.SelectionChanged -= OnSelectorChanged;
        if (_subscribedViewModel is null) return;
        _subscribedViewModel.Initialized -= OnViewModelInitialized;
        _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribedViewModel = null;
        UndoRefreshButtonAnimator.Cancel(UndoRefreshButton);
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
