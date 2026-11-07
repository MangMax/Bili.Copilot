// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Richasy.BiliKernel.Models;

namespace BiliCopilot.UI.Controls.Article;

/// <summary>
/// 文章分区头部.
/// </summary>
public sealed partial class ArticlePartitionMainHeader : ArticlePartitionDetailControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticlePartitionMainHeader"/> class.
    /// </summary>
    public ArticlePartitionMainHeader() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        Selector.SelectionChanged += OnSelectorChanged;
        if (ViewModel is null)
        {
            return;
        }

        InitializeChildPartitions();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.Initialized -= OnViewModelInitialized;
        }

        Selector.SelectionChanged -= OnSelectorChanged;
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(ArticlePartitionDetailViewModel? oldValue, ArticlePartitionDetailViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Initialized -= OnViewModelInitialized;
        }

        if (newValue is null)
        {
            return;
        }

        newValue.Initialized += OnViewModelInitialized;
        InitializeChildPartitions();
    }

    private void OnViewModelInitialized(object? sender, EventArgs e)
        => InitializeChildPartitions();

    private void OnSortTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Any()
            && e.AddedItems[0] is ArticleSortType sortType
            && sortType != ViewModel.SelectedSortType)
        {
            ViewModel.ChangeSortTypeCommand.Execute(sortType);
        }
    }

    private void OnSelectorChanged(object? sender, EventArgs args)
    {
        if (Selector.SelectedItem is null)
        {
            return;
        }

        var item = Selector.SelectedItem as PartitionViewModel;
        if (item is not null && item != ViewModel.CurrentPartition)
        {
            ViewModel.ChangeChildPartitionCommand.Execute(item);
        }
    }

    private void InitializeChildPartitions()
    {
        var items = new List<SelectorBarItem>();
        if (ViewModel.Children is not null)
        {
            foreach (var item in ViewModel.Children)
            {
                items.Add(new SelectorBarItem()
                {
                    Text = item.Title ?? default,
                    Tag = item,
                });
            }
        }

        var selected = ViewModel.IsRecommend
            ? items.FirstOrDefault()
            : items.FirstOrDefault(p => p.Tag == ViewModel.CurrentPartition);
        Selector.SetItems(items, selected?.Tag);
    }
}
