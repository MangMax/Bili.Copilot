// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Dispatching;

namespace BiliCopilot.UI.Controls.Popular;

/// <summary>
/// 流行视频页面侧边栏主体.
/// </summary>
public sealed partial class PopularSideBody : PopularPageControlBase
{
    private bool _selectionPending;
    private bool _selectionQueued;
    private bool _isActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="PopularSideBody"/> class.
    /// </summary>
    public PopularSideBody() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        _isActive = true;
        SectionView.ItemsSource = ViewModel.Sections;
        SectionView.LayoutUpdated += OnSectionLayoutUpdated;
        ViewModel.SectionInitialized += OnSectionInitialized;
        SectionView.SelectionChanged += OnSectionSelectionChanged;
        CheckSectionSelection();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        _isActive = false;
        _selectionPending = false;
        SectionView.LayoutUpdated -= OnSectionLayoutUpdated;
        SectionView.ItemsSource = default;
        ViewModel.SectionInitialized -= OnSectionInitialized;
        SectionView.SelectionChanged -= OnSectionSelectionChanged;
    }

    private void OnSectionInitialized(object? sender, EventArgs e)
        => CheckSectionSelection();

    private void OnSectionSelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        var item = sender.SelectedItem as IPopularSectionItemViewModel;
        ViewModel.SelectSectionCommand.Execute(item);
    }

    private void CheckSectionSelection()
    {
        _selectionPending = true;
        QueueSectionSelection();
    }

    private void OnSectionLayoutUpdated(object? sender, object e)
        => QueueSectionSelection();

    private void QueueSectionSelection()
    {
        if (!_isActive || !_selectionPending || _selectionQueued
            || ViewModel.SelectedSection is null || !SectionView.IsLoaded || SectionView.ActualWidth <= 0)
        {
            return;
        }

        // Never mutate ItemsView selection inside its layout/collection notification.
        _selectionQueued = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _selectionQueued = false;
            if (!_isActive || !SectionView.IsLoaded)
            {
                return;
            }

            var index = ViewModel.Sections.IndexOf(ViewModel.SelectedSection);
            if (index >= 0)
            {
                _selectionPending = false;
                SectionView.Select(index);
            }
        });
    }
}
