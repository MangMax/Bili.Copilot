// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Input;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.Toolkits;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 文章卡片控件.
/// </summary>
public sealed partial class ArticleCardControl : LayoutControlBase<ArticleItemViewModel>
{
    private CardControl _rootCard;
    private Button? _userButton;
    private Button? _authorButton;
    private Button? _moreButton;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleCardControl"/> class.
    /// </summary>
    public ArticleCardControl() => DefaultStyleKey = typeof(ArticleCardControl);

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        if (_moreButton is not null)
        {
            _moreButton.Click -= OnMoreClick;
        }

        _rootCard = GetTemplateChild("RootCard") as CardControl;
        _userButton = GetTemplateChild("UserButton") as Button;
        _authorButton = GetTemplateChild("AuthorButton") as Button;
        _moreButton = GetTemplateChild("MoreButton") as Button;
        if (_moreButton is not null)
        {
            _moreButton.Click += OnMoreClick;
        }

        ApplyViewModelCommands(ViewModel);
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
        => ContextRequested += OnContextRequested;

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        ContextRequested -= OnContextRequested;
        ContextFlyout?.Hide();
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(ArticleItemViewModel? oldValue, ArticleItemViewModel? newValue)
    {
        ContextFlyout?.Hide();
        ContextFlyout = null;
        ApplyViewModelCommands(newValue);
    }

    private void ApplyViewModelCommands(ArticleItemViewModel? viewModel)
    {
        if (_rootCard is not null)
        {
            _rootCard.Command = viewModel?.OpenReaderCommand;
        }

        if (_userButton is not null)
        {
            _userButton.Command = viewModel?.ShowUserSpaceCommand;
            _userButton.IsEnabled = viewModel?.CanOpenUserSpace == true;
        }

        if (_authorButton is not null)
        {
            _authorButton.Command = viewModel?.ShowUserSpaceCommand;
            _authorButton.IsEnabled = viewModel?.CanOpenUserSpace == true;
        }
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

    private static MenuFlyoutItem CreateEnterUserSpaceItem()
    {
        return new MenuFlyoutItem()
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.EnterUserSpace),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.Person },
            Tag = nameof(ViewModel.ShowUserSpaceCommand),
        };
    }

    private static MenuFlyoutItem CreatePinItem()
    {
        return new MenuFlyoutItem()
        {
            Text = ResourceToolkit.GetLocalizedString(StringNames.FixContent),
            Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = FluentIcons.Common.Symbol.Pin },
            Tag = nameof(ViewModel.PinCommand),
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

    private void OnMoreClick(object sender, RoutedEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (ContextFlyout is null)
        {
            CreateContextFlyout();
        }

        RelocateCommands();
        if (sender is FrameworkElement target)
        {
            ContextFlyout?.ShowAt(target);
        }
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
        menuFlyout.Items.Add(CreateOpenInBroswerItem());
        if (ViewModel.CanOpenUserSpace)
        {
            menuFlyout.Items.Add(CreateEnterUserSpaceItem());
        }

        menuFlyout.Items.Add(CreatePinItem());
        if (ViewModel.Style == ArticleCardStyle.History)
        {
            menuFlyout.Items.Add(CreateRemoveHistoryItem());
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
                case nameof(ViewModel.OpenInBroswerCommand):
                    item.Command = ViewModel.OpenInBroswerCommand;
                    break;
                case nameof(ViewModel.ShowUserSpaceCommand):
                    item.Command = ViewModel.ShowUserSpaceCommand;
                    break;
                case nameof(ViewModel.PinCommand):
                    item.Command = ViewModel.PinCommand;
                    break;
                case nameof(ViewModel.RemoveHistoryCommand):
                    item.Command = ViewModel.RemoveHistoryCommand;
                    break;
                default:
                    break;
            }
        }
    }
}
