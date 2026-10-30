// Copyright (c) Bili Copilot. All rights reserved.

using System.ComponentModel;
using BiliCopilot.UI.Controls.Components;

namespace BiliCopilot.UI.Controls.Popular;

/// <summary>
/// 流行视频页面主标题.
/// </summary>
public sealed partial class PopularMainHeader : PopularPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PopularMainHeader"/> class.
    /// </summary>
    public PopularMainHeader()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyUndoRefreshVisibility(animate: false);
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.CanUndoRefresh))
        {
            ApplyUndoRefreshVisibility(animate: true);
        }
    }

    private void ApplyUndoRefreshVisibility(bool animate)
        => UndoRefreshButtonAnimator.Apply(
            UndoRefreshButton,
            ViewModel.CanUndoRefresh,
            animate,
            () => !ViewModel.CanUndoRefresh);
}
