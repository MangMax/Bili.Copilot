// Copyright (c) Bili Copilot. All rights reserved.

using System.ComponentModel;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.ViewModels.View;

namespace BiliCopilot.UI.Controls.Popular;

/// <summary>
/// 流行视频页面主标题.
/// </summary>
public sealed partial class PopularMainHeader : PopularPageControlBase
{
    private PopularPageViewModel? _subscribedViewModel;

    public PopularMainHeader()
    {
        InitializeComponent();
        // The restored NuGet base unregisters its lifecycle handlers on first unload.
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    protected override void OnViewModelChanged(PopularPageViewModel? oldValue, PopularPageViewModel? newValue)
    {
        Detach();
        if (IsLoaded) Attach();
    }

    private void Attach()
    {
        Detach();
        _subscribedViewModel = ViewModel;
        if (_subscribedViewModel is null) return;
        _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyUndoRefreshVisibility(animate: false);
    }

    private void Detach()
    {
        if (_subscribedViewModel is null) return;
        _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribedViewModel = null;
        UndoRefreshButtonAnimator.Apply(UndoRefreshButton, UndoRefreshButton.Visibility == Visibility.Visible, false);
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
