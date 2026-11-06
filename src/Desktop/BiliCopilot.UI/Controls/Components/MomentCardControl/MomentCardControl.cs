// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 动态卡片展示器.
/// </summary>
public abstract class MomentCardPresenter : LayoutUserControlBase<MomentItemViewModel>;

/// <summary>
/// 动态卡片控件.
/// </summary>
public sealed partial class MomentCardControl : LayoutControlBase<MomentItemViewModel>
{
    private bool _hovered;
    private VideoCardControl? _videoCard;
    private TranslateTransform? _lift;
    private Storyboard? _liftAnimation;

    /// <summary>Initializes a new instance of the <see cref="MomentCardControl"/> class.</summary>
    public MomentCardControl()
    {
        DefaultStyleKey = typeof(MomentCardControl);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Opens the actions for the current dynamic from its header.</summary>
    public void ShowMore(FrameworkElement target)
    {
        CreateContextFlyout();
        RelocateCommands();
        ContextFlyout.ShowAt(target);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ContextRequested += OnContextRequested;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        UpdateSurface();
    }

    protected override void OnApplyTemplate()
    {
        _liftAnimation?.Stop();
        _lift = GetTemplateChild("MomentLift") as TranslateTransform;
        if (_lift is not null) _lift.Y = _hovered ? -2 : 0;
        UpdateSurface();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        ContextRequested -= OnContextRequested;
        PointerEntered -= OnPointerEntered;
        PointerExited -= OnPointerExited;
        _videoCard?.SetMomentHovered(false);
        _videoCard = null;
        _hovered = false;
        ResetLift();
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(MomentItemViewModel? oldValue, MomentItemViewModel? newValue)
    {
        ContextFlyout?.Hide();
        ContextFlyout = null;
        _videoCard?.SetMomentHovered(false);
        _videoCard = null;
        _hovered = false;
        ResetLift();
        UpdateSurface();
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs args) => SetHovered(true);

    private void OnPointerExited(object sender, PointerRoutedEventArgs args) => SetHovered(false);

    private void SetHovered(bool hovered)
    {
        if (_hovered == hovered) return;
        _hovered = hovered;
        UpdateSurface();
        if (_lift is null) return;
        var currentY = _lift.Y;
        _liftAnimation?.Stop();
        _lift.Y = currentY;
        var animation = new DoubleAnimation
        {
            To = hovered ? -2 : 0,
            Duration = TimeSpan.FromMilliseconds(160),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, _lift);
        Storyboard.SetTargetProperty(animation, nameof(TranslateTransform.Y));
        _liftAnimation = new Storyboard();
        _liftAnimation.Children.Add(animation);
        _liftAnimation.Begin();
    }

    private void ResetLift()
    {
        _liftAnimation?.Stop();
        _liftAnimation = null;
        if (_lift is not null) _lift.Y = 0;
    }

    private void UpdateSurface()
    {
        VisualStateManager.GoToState(this, _hovered ? "MomentHovered" : "MomentNormal", false);
        if (ViewModel?.InnerContent is VideoItemViewModel video)
        {
            if (_videoCard?.ViewModel != video) _videoCard = FindVideo(this);
            if (_videoCard?.ViewModel == video)
            {
                _videoCard.MinimumTitleHeight = ViewModel.Style == MomentCardStyle.Video ? 50 : 0;
                _videoCard.ShowDescription = ViewModel.Style == MomentCardStyle.Comprehensive;
                _videoCard.SetMomentHovered(_hovered);
            }
        }
    }

    private static VideoCardControl? FindVideo(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is VideoCardControl video) return video;
            if (FindVideo(child) is { } nested) return nested;
        }
        return null;
    }

    private static MenuFlyoutItem CreatePrivatePlayItem()
    {
        return new MenuFlyoutItem()
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.PlayInPrivate),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.EyeOff },
            Tag = nameof(ViewModel.PlayInPrivateCommand),
        };
    }

    private static MenuFlyoutItem CreateUserSpaceItem()
    {
        return new MenuFlyoutItem()
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.EnterUserSpace),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.Person },
            Tag = nameof(ViewModel.ShowUserSpaceCommand),
        };
    }

    private static MenuFlyoutItem CreateAddViewLaterItem()
    {
        return new MenuFlyoutItem()
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.AddToViewLater),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.CalendarAdd },
            Tag = nameof(ViewModel.AddToViewLaterCommand),
        };
    }

    private static MenuFlyoutItem CreateOpenInBroswerItem()
    {
        return new MenuFlyoutItem()
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.OpenInBrowser),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.Globe },
            Tag = nameof(ViewModel.OpenInBroswerCommand),
        };
    }

    private static MenuFlyoutItem CreateCopyUriItem()
    {
        return new MenuFlyoutItem()
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.CopyVideoUrl),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.Copy },
            Tag = nameof(ViewModel.CopyUrlCommand),
        };
    }

    private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        CreateContextFlyout();
        RelocateCommands();
        args.TryGetPosition(this, out var point);
        ContextFlyout.ShowAt(this, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = point });
        args.Handled = true;
    }

    private void CreateContextFlyout()
    {
        var menuFlyout = new MenuFlyout() { ShouldConstrainToRootBounds = false };
        if (ViewModel.VideoCover is not null)
        {
            menuFlyout.Items.Add(CreatePrivatePlayItem());
            menuFlyout.Items.Add(CreateOpenInBroswerItem());
            menuFlyout.Items.Add(CreateUserSpaceItem());
            menuFlyout.Items.Add(CreateAddViewLaterItem());
            menuFlyout.Items.Add(CreateCopyUriItem());
        }
        else
        {
            menuFlyout.Items.Add(CreateOpenInBroswerItem());
        }

        menuFlyout.Items.Add(new MenuFlyoutItem
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.ShareLink),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.Share },
            Command = ViewModel.CopyMomentLinkCommand,
        });
        ContextFlyout = menuFlyout;
    }

    private void RelocateCommands()
    {
        if (ContextFlyout is not MenuFlyout flyout)
        {
            return;
        }

        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            switch (item.Tag?.ToString())
            {
                case nameof(ViewModel.PlayInPrivateCommand):
                    item.Command = ViewModel.PlayInPrivateCommand;
                    break;
                case nameof(ViewModel.ShowUserSpaceCommand):
                    item.Command = ViewModel.ShowUserSpaceCommand;
                    break;
                case nameof(ViewModel.AddToViewLaterCommand):
                    item.Command = ViewModel.AddToViewLaterCommand;
                    break;
                case nameof(ViewModel.OpenInBroswerCommand):
                    item.Command = ViewModel.OpenInBroswerCommand;
                    break;
                case nameof(ViewModel.CopyUrlCommand):
                    item.Command = ViewModel.CopyUrlCommand;
                    break;
                default:
                    break;
            }
        }
    }
}
