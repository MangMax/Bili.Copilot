// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Controls.LivePartition;

/// <summary>
/// 直播子分区选择器.
/// </summary>
public sealed partial class LiveSubPartitionSelector : LiveSubPartitionControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSubPartitionSelector"/> class.
    /// </summary>
    public LiveSubPartitionSelector() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        Selector.SelectionChanged += OnSelectorChanged;
        if (ViewModel is null)
        {
            return;
        }

        InitializeTags();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        Selector.SelectionChanged -= OnSelectorChanged;
        if (ViewModel is not null)
        {
            ViewModel.Initialized -= OnViewModelInitialized;
        }
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(LivePartitionDetailViewModel? oldValue, LivePartitionDetailViewModel? newValue)
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
        InitializeTags();
    }

    private void OnViewModelInitialized(object? sender, EventArgs e)
        => InitializeTags();

    private void OnSelectorChanged(object? sender, EventArgs args)
    {
        if (Selector.SelectedItem is LiveTag item && item != ViewModel.CurrentTag)
        {
            ViewModel.ChangeChildPartitionCommand.Execute(item);
        }
    }

    private void InitializeTags()
    {
        if (ViewModel?.AvailableTags is not { } children)
        {
            // 没有子分区（首次加载失败或该分区没有标签）时收起选择器：
            // 空列表加上失效选中项不能交给 SlidingTabBar。
            Selector.Visibility = Visibility.Collapsed;
            return;
        }

        Selector.Visibility = Visibility.Visible;
        var items = new List<SelectorBarItem>(children.Count);
        foreach (var item in children)
        {
            items.Add(new SelectorBarItem()
            {
                Text = item.Name ?? default,
                Tag = item,
            });
        }

        var selected = items.FirstOrDefault(p => p.Tag == ViewModel.CurrentTag);
        Selector.SetItems(items, selected?.Tag);
    }
}
