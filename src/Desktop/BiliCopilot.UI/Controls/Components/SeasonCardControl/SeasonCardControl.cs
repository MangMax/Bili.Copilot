// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Icons;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.Toolkits;
using System.Windows.Input;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 剧集卡片展示器.
/// </summary>
public abstract class SeasonCardPresenter : LayoutUserControlBase<SeasonItemViewModel>;

/// <summary>
/// 剧集卡片控件.
/// </summary>
public sealed partial class SeasonCardControl : LayoutControlBase<SeasonItemViewModel>
{
    private ButtonBase _rootCard;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonCardControl"/> class.
    /// </summary>
    public SeasonCardControl() => DefaultStyleKey = typeof(SeasonCardControl);

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        _rootCard = GetTemplateChild("RootCard") as ButtonBase;
        if (ViewModel is not null)
        {
            _rootCard?.Command = ViewModel.PlayCommand;
        }
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
        => ContextRequested += OnContextRequested;

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
        => ContextRequested -= OnContextRequested;

    /// <inheritdoc/>
    protected override void OnViewModelChanged(SeasonItemViewModel? oldValue, SeasonItemViewModel? newValue)
    {
        if (_rootCard is not null)
        {
            _rootCard.Command = newValue?.PlayCommand;
        }
    }

    private async void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        args.Handled = true;
        var flyout = new MenuFlyout { ShouldConstrainToRootBounds = false };
        flyout.Items.Add(CreateItem(StringNames.OpenInBrowser, IconGlyph.Globe, viewModel.OpenInBroswerCommand));

        var isFavorite = viewModel.Style == SeasonCardStyle.Favorite;
        var trackingItem = new MenuFlyoutItem { Text = ResourceToolkit.GetLocalizedString(StringNames.Loading), IsEnabled = false };

        if (!isFavorite)
        {
            flyout.Items.Add(trackingItem);
        }

        flyout.Items.Add(CreateItem(StringNames.FixContent, IconGlyph.Pin, viewModel.PinCommand));
        if (isFavorite)
        {
            flyout.Items.Add(CreateItem(StringNames.MarkWantWatch, IconGlyph.Thinking, viewModel.MarkWantWatchCommand, viewModel.InWantWatch != true));
            flyout.Items.Add(CreateItem(StringNames.MarkWatching, IconGlyph.Eye, viewModel.MarkWatchingCommand, viewModel.InWatching != true));
            flyout.Items.Add(CreateItem(StringNames.MarkWatched, IconGlyph.Checkmark, viewModel.MarkWatchedCommand, viewModel.InWatched != true));
            SetTrackingItem(trackingItem, viewModel, true);
            flyout.Items.Add(trackingItem);
        }

        var options = new FlyoutShowOptions();
        if (args.TryGetPosition(this, out var point))
        {
            options.Position = point;
        }

        flyout.ShowAt(this, options);
        if (!isFavorite)
        {
            if (await viewModel.GetTrackingStateAsync() is { } isTracking)
            {
                SetTrackingItem(trackingItem, viewModel, isTracking);
            }
            else
            {
                trackingItem.Text = ResourceToolkit.GetLocalizedString(StringNames.RequestPgcFailed);
            }
        }
    }

    private void SetTrackingItem(MenuFlyoutItem item, SeasonItemViewModel viewModel, bool isTracking)
    {
        item.Text = ResourceToolkit.GetLocalizedString(isTracking ? StringNames.PgcUnfollow : StringNames.PgcFollow);
        var icon = new FontIcon { FontFamily = IconGlyph.FontFamily, Glyph = isTracking ? IconGlyph.HeartBroken : IconGlyph.Heart };
        if (isTracking)
        {
            icon.Foreground = this.Get<IResourceToolkit>().GetThemeBrush("SystemFillColorCriticalBrush");
        }

        item.Icon = icon;
        item.Command = isTracking ? viewModel.UnfollowCommand : viewModel.FollowCommand;
        item.IsEnabled = true;
    }

    private static MenuFlyoutItem CreateItem(StringNames label, string glyph, ICommand command, bool isEnabled = true)
        => new()
        {
            Text = ResourceToolkit.GetLocalizedString(label),
            Icon = new FontIcon { FontFamily = IconGlyph.FontFamily, Glyph = glyph },
            Command = command,
            IsEnabled = isEnabled,
        };
}
