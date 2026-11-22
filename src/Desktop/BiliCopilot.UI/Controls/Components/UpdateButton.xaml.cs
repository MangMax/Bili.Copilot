// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 标题栏上的更新入口.
/// </summary>
public sealed partial class UpdateButton : UpdateButtonBase
{
    private DispatcherQueueTimer? _hideInfoTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateButton"/> class.
    /// </summary>
    public UpdateButton()
    {
        InitializeComponent();
        Unloaded += (_, _) => HideInfo();
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
        => _ = ViewModel.AutoCheckAsync();

    private void OnButtonPointerEntered(object sender, PointerRoutedEventArgs e)
        => ShowInfo();

    private void OnButtonPointerExited(object sender, PointerRoutedEventArgs e)
        => ScheduleHideInfo();

    private void OnInfoPointerEntered(object sender, PointerRoutedEventArgs e)
        => _hideInfoTimer?.Stop();

    private void OnInfoPointerExited(object sender, PointerRoutedEventArgs e)
        => ScheduleHideInfo();

    private void OnActionClick(object sender, RoutedEventArgs e)
        => HideInfo();

    private void ShowInfo()
    {
        _hideInfoTimer?.Stop();
        if (FlyoutBase.GetAttachedFlyout(this) is Flyout { IsOpen: false } flyout)
        {
            // 悬浮说明打开时仍允许点击按钮本身，避免第一次点击只用于关闭浮层.
            flyout.OverlayInputPassThroughElement = ActionButton;
            flyout.ShowAt(ActionButton, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
        }
    }

    private void ScheduleHideInfo()
    {
        if (_hideInfoTimer is null)
        {
            _hideInfoTimer = DispatcherQueue.CreateTimer();
            // 留出从按钮移动到浮层内继续滚动的时间，移出浮层后才真正收起.
            _hideInfoTimer.Interval = TimeSpan.FromMilliseconds(200);
            _hideInfoTimer.IsRepeating = false;
            _hideInfoTimer.Tick += (_, _) => HideInfo();
        }

        _hideInfoTimer.Stop();
        _hideInfoTimer.Start();
    }

    private void HideInfo()
    {
        _hideInfoTimer?.Stop();
        if (FlyoutBase.GetAttachedFlyout(this) is Flyout { IsOpen: true } flyout)
        {
            flyout.Hide();
        }
    }
}

/// <summary>
/// 更新入口控件的基类.
/// </summary>
public abstract class UpdateButtonBase : LayoutUserControlBase<AppUpdateViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateButtonBase"/> class.
    /// </summary>
    protected UpdateButtonBase() => ViewModel = this.Get<AppUpdateViewModel>();
}
