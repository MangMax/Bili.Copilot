// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using Microsoft.Extensions.Logging;
using BiliCopilot.UI.ViewModels.Components;
using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.Toolkits;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 视频卡片控件.
/// </summary>
public sealed partial class VideoCardControl : LayoutControlBase<VideoItemViewModel>
{
    private CompositeTransform? _popularCoverZoom;
    private Storyboard? _coverAnimation;
    private ButtonBase _rootCard;
    private Button _userButton;
    private Button _authorButton;
    private Button? _moreButton;
    private CancellationTokenSource? _authorRequest;
    private AccountViewModel? _account;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoCardControl"/> class.
    /// </summary>
    public VideoCardControl()
    {
        DefaultStyleKey = typeof(VideoCardControl);
        Loaded += OnCardLoaded;
        Unloaded += OnCardUnloaded;
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        if (_moreButton is not null) _moreButton.Click -= OnMoreClick;
        _moreButton = GetTemplateChild("MoreButton") as Button;
        if (_moreButton is not null) _moreButton.Click += OnMoreClick;
        if (_rootCard is not null)
        {
            _rootCard.PointerEntered -= OnPopularPointerEntered;
            _rootCard.PointerExited -= OnPopularPointerExited;
        }
        _coverAnimation?.Stop();
        _rootCard = GetTemplateChild("RootCard") as ButtonBase;
        _popularCoverZoom = GetTemplateChild("PopularCoverZoom") as CompositeTransform;
        if (_rootCard is not null && _popularCoverZoom is not null)
        {
            _rootCard.PointerEntered += OnPopularPointerEntered;
            _rootCard.PointerExited += OnPopularPointerExited;
        }
        _userButton = GetTemplateChild("UserButton") as Button;
        _authorButton = GetTemplateChild("AuthorButton") as Button;
        if (IsLoaded) RefreshPopularCardData();
        if (ViewModel is not null)
        {
            if (_rootCard is not null)
            {
                _rootCard.Command = ViewModel.PlayCommand;
            }

            if (_userButton is not null)
            {
                _userButton.Command = ViewModel.ShowUserSpaceCommand;
                _userButton.IsEnabled = ViewModel.CanOpenUserSpace;
            }

            if (_authorButton is not null)
            {
                _authorButton.Command = ViewModel.ShowUserSpaceCommand;
                _authorButton.IsEnabled = ViewModel.CanOpenUserSpace;
            }
        }
    }

    private void OnPopularPointerEntered(object sender, PointerRoutedEventArgs args) => AnimatePopularCover(true);

    private void OnPopularPointerExited(object sender, PointerRoutedEventArgs args) => AnimatePopularCover(false);

    private void AnimatePopularCover(bool hovered)
    {
        if (_popularCoverZoom is null) return;
        _coverAnimation?.Stop();
        var story = new Storyboard();
        foreach (var property in new[] { "ScaleX", "ScaleY" })
        {
            var animation = new DoubleAnimation
            {
                To = hovered ? 1.025 : 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(animation, _popularCoverZoom);
            Storyboard.SetTargetProperty(animation, property);
            story.Children.Add(animation);
        }
        _coverAnimation = story;
        story.Begin();
        VisualStateManager.GoToState(this, hovered ? "PopularPointerOver" : "PopularNormal", false);
    }

    private void ResetPopularCover()
    {
        _coverAnimation?.Stop();
        _coverAnimation = null;
        if (_popularCoverZoom is not null) _popularCoverZoom.ScaleX = _popularCoverZoom.ScaleY = 1;
        VisualStateManager.GoToState(this, "PopularNormal", false);
    }

    private void OnCardLoaded(object sender, RoutedEventArgs args)
    {
        ContextRequested -= OnContextRequested;
        ContextRequested += OnContextRequested;
        RefreshPopularCardData();
    }

    private void OnCardUnloaded(object sender, RoutedEventArgs args)
    {
        ResetPopularCover();
        ContextRequested -= OnContextRequested;
        if (_account is not null) _account.PropertyChanged -= OnAccountChanged;
        _account = null;
        _authorRequest?.Cancel();
        _authorRequest?.Dispose();
        _authorRequest = null;
        ContextFlyout?.Hide();
    }

    private void OnAccountChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(AccountViewModel.MyProfile) or null or "")
        {
            // Following is account-specific; the shared batcher invalidates old-account results.
            if (ViewModel is not null) ViewModel.IsAuthorFollowed = null;
            RefreshPopularCardData();
        }
    }

    private void RefreshPopularCardData()
    {
        _authorRequest?.Cancel();
        _authorRequest?.Dispose();
        _authorRequest = null;
        if (!IsLoaded || _moreButton is null || ViewModel is not { IsFeedCard: true } model)
        {
            if (_account is not null) _account.PropertyChanged -= OnAccountChanged;
            _account = null;
            return;
        }
        _account ??= this.Get<AccountViewModel>();
        _account.PropertyChanged -= OnAccountChanged;
        _account.PropertyChanged += OnAccountChanged;
        var request = new CancellationTokenSource();
        _authorRequest = request;
        _ = LoadPopularFollowingAsync(model, request.Token);
        _ = LoadPopularMetadataAsync(model, request.Token);
    }

    private async Task LoadPopularFollowingAsync(VideoItemViewModel model, CancellationToken token)
    {
        try
        {
            await model.LoadPopularFollowingAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Relationship failure must not prevent the card's existing play/menu commands.
            this.Get<ILogger<VideoCardControl>>().LogWarning(ex, "Unable to complete popular-card following state.");
        }
    }

    private async Task LoadPopularMetadataAsync(VideoItemViewModel model, CancellationToken token)
    {
        try { await model.LoadPopularMetadataAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            this.Get<ILogger<VideoCardControl>>().LogWarning(ex, "Unable to complete popular-card video metadata.");
        }
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(VideoItemViewModel? oldValue, VideoItemViewModel? newValue)
    {
        ResetPopularCover();
        ContextFlyout?.Hide();
        ContextFlyout = null;
        RefreshPopularCardData();
        if (_rootCard is not null)
        {
            _rootCard.Command = newValue?.PlayCommand;
        }

        if (_userButton is not null)
        {
            _userButton.Command = newValue?.ShowUserSpaceCommand;
            _userButton.IsEnabled = newValue?.CanOpenUserSpace ?? false;
        }

        if (_authorButton is not null)
        {
            _authorButton.Command = newValue?.ShowUserSpaceCommand;
            _authorButton.IsEnabled = newValue?.CanOpenUserSpace ?? false;
        }
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

    private MenuFlyoutItem CreateRemoveViewLaterItem()
    {
        return new MenuFlyoutItem()
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.Remove),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.Delete, Foreground = this.Get<IResourceToolkit>().GetThemeBrush("SystemFillColorCriticalBrush") },
            Tag = nameof(ViewModel.RemoveViewLaterCommand),
        };
    }

    private MenuFlyoutItem CreateRemoveHistoryItem()
    {
        return new MenuFlyoutItem()
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.Remove),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.Delete, Foreground = this.Get<IResourceToolkit>().GetThemeBrush("SystemFillColorCriticalBrush") },
            Tag = nameof(ViewModel.RemoveHistoryCommand),
        };
    }

    private MenuFlyoutItem CreateRemoveFavoriteItem()
    {
        return new MenuFlyoutItem() { Text = ResourceToolkit.GetLocalizedString(StringNames.Remove), Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.Delete, Foreground = this.Get<IResourceToolkit>().GetThemeBrush("SystemFillColorCriticalBrush") }, Tag = nameof(ViewModel.RemoveFavoriteCommand) };
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
            Tag = nameof(ViewModel.CopyUriCommand),
        };
    }

    private void OnMoreClick(object sender, RoutedEventArgs args)
    {
        if (ViewModel is null) return;
        CreateContextFlyout();
        RelocateCommands();
        ContextFlyout.ShowAt((FrameworkElement)sender);
    }

    private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (ContextFlyout is null)
        {
            CreateContextFlyout();
            args.TryGetPosition(this, out var point);
            ContextFlyout.ShowAt(this, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = point });
        }

        RelocateCommands();
        args.Handled = true;
    }

    private void CreateContextFlyout()
    {
        var menuFlyout = new MenuFlyout() { ShouldConstrainToRootBounds = false };
        menuFlyout.Items.Add(CreatePrivatePlayItem());
        if (ViewModel.Style != VideoCardStyle.Moment && ViewModel.CanOpenUserSpace)
        {
            menuFlyout.Items.Add(CreateUserSpaceItem());
        }

        if (ViewModel.Style != VideoCardStyle.ViewLater)
        {
            menuFlyout.Items.Add(CreateAddViewLaterItem());
        }

        menuFlyout.Items.Add(CreateOpenInBroswerItem());
        menuFlyout.Items.Add(CreateCopyUriItem());
        if (ViewModel.Style == VideoCardStyle.ViewLater)
        {
            menuFlyout.Items.Add(CreateRemoveViewLaterItem());
        }
        else if (ViewModel.Style == VideoCardStyle.History)
        {
            menuFlyout.Items.Add(CreateRemoveHistoryItem());
        }
        else if (ViewModel.Style == VideoCardStyle.Favorite)
        {
            menuFlyout.Items.Add(CreateRemoveFavoriteItem());
        }

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
            switch (item.Tag.ToString())
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
                case nameof(ViewModel.RemoveViewLaterCommand):
                    item.Command = ViewModel.RemoveViewLaterCommand;
                    break;
                case nameof(ViewModel.RemoveHistoryCommand):
                    item.Command = ViewModel.RemoveHistoryCommand;
                    break;
                case nameof(ViewModel.RemoveFavoriteCommand):
                    item.Command = ViewModel.RemoveFavoriteCommand;
                    break;
                case nameof(ViewModel.OpenInBroswerCommand):
                    item.Command = ViewModel.OpenInBroswerCommand;
                    break;
                case nameof(ViewModel.CopyUriCommand):
                    item.Command = ViewModel.CopyUriCommand;
                    break;
                default:
                    break;
            }
        }
    }
}
