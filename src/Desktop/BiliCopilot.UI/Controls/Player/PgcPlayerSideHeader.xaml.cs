// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// PGC播放页侧边栏头部.
/// </summary>
public sealed partial class PgcPlayerSideHeader : PgcPlayerControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcPlayerSideHeader"/> class.
    /// </summary>
    public PgcPlayerSideHeader() => InitializeComponent();

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
            ViewModel.SectionInitialized -= OnViewModelSectionInitialized;
        }

        Selector.SelectionChanged -= OnSelectorChanged;
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(PgcConnectorViewModel? oldValue, PgcConnectorViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.SectionInitialized -= OnViewModelSectionInitialized;
        }

        if (newValue is null)
        {
            return;
        }

        newValue.SectionInitialized += OnViewModelSectionInitialized;
        InitializeChildPartitions();
    }

    private void OnViewModelSectionInitialized(object? sender, EventArgs e)
        => InitializeChildPartitions();

    private void OnSelectorChanged(object? sender, EventArgs args)
    {
        if (Selector.SelectedItem is IPlayerSectionDetailViewModel item)
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
                    Text = item.Title ?? default,
                    Tag = item,
                });
            }
        }

        Selector.SetItems(items, ViewModel.SelectedSection);
    }
}
