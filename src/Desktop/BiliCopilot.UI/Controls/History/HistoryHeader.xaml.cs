// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using Richasy.BiliKernel.Models;

namespace BiliCopilot.UI.Controls.History;

/// <summary>
/// 历史记录页头部.
/// </summary>
public sealed partial class HistoryHeader : HistoryPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryHeader"/> class.
    /// </summary>
    public HistoryHeader() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        Selector.SelectionChanged += OnSelectorChanged;
        ViewModel.SectionInitialized += OnViewModelSectionInitialized;
        InitializeChildPartitions();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.SectionInitialized -= OnViewModelSectionInitialized;
        }

        Selector.SelectionChanged -= OnSelectorChanged;
    }

    private static string GetSectionName(ViewHistoryTabType type)
    {
        return type switch
        {
            ViewHistoryTabType.Video => ResourceToolkit.GetLocalizedString(StringNames.Video),
            ViewHistoryTabType.Article => ResourceToolkit.GetLocalizedString(StringNames.Article),
            ViewHistoryTabType.Live => ResourceToolkit.GetLocalizedString(StringNames.Live),
            _ => string.Empty,
        };
    }

    private void OnViewModelSectionInitialized(object? sender, EventArgs e)
        => InitializeChildPartitions();

    private void OnSelectorChanged(object? sender, EventArgs args)
    {
        if (Selector.SelectedItem is null)
        {
            return;
        }

        var item = Selector.SelectedItem as IHistorySectionDetailViewModel;
        if (item is not null)
        {
            ViewModel.SelectSectionCommand.Execute(item);
        }
    }

    private void InitializeChildPartitions()
    {
        var items = new List<SelectorBarItem>();
        if (ViewModel.Sections is not null)
        {
            foreach (var item in ViewModel.Sections)
            {
                items.Add(new SelectorBarItem()
                {
                    Text = GetSectionName(item.Type),
                    Tag = item,
                });
            }
        }

        Selector.SetItems(items, ViewModel.SelectedSection);
    }

    private void OnCleanButtonClick(object sender, RoutedEventArgs e)
        => CleanTip.IsOpen = true;
}
