// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Richasy.WinUIKernel.Share.Base;
using System.Globalization;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 位于标题栏的账户控件.
/// </summary>
public sealed partial class HeaderAccountControl : HeaderAccountControlBase
{
    private TextBlock? _accountSwitchCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderAccountControl"/> class.
    /// </summary>
    public HeaderAccountControl() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
        => ViewModel.InitializeCommand.Execute(default);

    private void OnFlyoutOpened(object sender, object e)
    {
        var accounts = GlobalDependencies.Accounts;
        AccountSwitchMenu.Items.Clear();
        foreach (var account in accounts.SavedAccounts.OrderByDescending(item => item.Uid == accounts.CurrentUid).ThenBy(item => item.Name))
        {
            var current = account.Uid == accounts.CurrentUid;
            var item = new ToggleMenuFlyoutItem
            {
                Text = string.IsNullOrWhiteSpace(account.Name) ? $"UID {account.Uid}" : $"{account.Name} · {account.Uid}",
                IsChecked = current,
                IsEnabled = !current,
            };
            if (Uri.TryCreate(account.AvatarUrl, UriKind.Absolute, out var avatar) && avatar.Scheme == Uri.UriSchemeHttps)
            {
                item.Icon = new ImageIcon { Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(avatar) };
            }

            item.Click += async (_, _) => await SwitchAccountAsync(account.Uid);
            AccountSwitchMenu.Items.Add(item);
        }

        UpdateAccountSwitchCount();
        ViewModel.UpdateCommunityInformationCommand.Execute(default);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => ViewModel.IsFlyoutOpened = true);
    }

    private void OnAccountSwitchMenuLoaded(object sender, RoutedEventArgs e) => UpdateAccountSwitchCount();

    private void UpdateAccountSwitchCount()
    {
        // Keep WinUI's submenu template and place the count in a column before its chevron.
        if (FindSubmenuChevron(AccountSwitchMenu) is not { Parent: Grid layout } chevron)
        {
            return;
        }

        if (_accountSwitchCount is not { } count || !ReferenceEquals(count.Parent, layout))
        {
            var column = Grid.GetColumn(chevron);
            layout.ColumnDefinitions.Insert(column, new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(chevron, column + 1);
            var margin = chevron.Margin;
            chevron.Margin = new Thickness(8, margin.Top, margin.Right, margin.Bottom);
            count = new TextBlock
            {
                FontSize = 12,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            count.SetBinding(TextBlock.ForegroundProperty, new Binding { Source = chevron, Path = new PropertyPath(nameof(FontIcon.Foreground)) });
            AutomationProperties.SetAutomationId(count, "AccountSwitchCount");
            Grid.SetColumn(count, column);
            layout.Children.Add(count);
            _accountSwitchCount = count;
        }

        count.Text = AccountSwitchMenu.Items.Count.ToString(CultureInfo.CurrentCulture);
    }

    private static FontIcon? FindSubmenuChevron(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is FontIcon { Name: "SubItemChevron" } chevron)
            {
                return chevron;
            }

            if (FindSubmenuChevron(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static async Task SwitchAccountAsync(long uid)
    {
        try
        {
            await GlobalDependencies.Accounts.SwitchAsync(uid);
        }
        catch (Exception ex) when (AccountProfileLoadFailureClassifier.Classify(ex) == AccountProfileLoadFailureKind.AuthenticationExpired)
        {
            GlobalDependencies.Accounts.OpenSignIn(uid);
        }
        catch (Exception)
        {
            await GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().ShowTipCommand.ExecuteAsync(
                (ResourceToolkit.GetLocalizedString(StringNames.SwitchBiliAccountFailed), InfoType.Error));
        }
    }

    private void OnAddAccountClick(object sender, RoutedEventArgs e)
        => GlobalDependencies.Accounts.OpenSignIn();

    private void OnFlyoutClosed(object sender, object e)
    {
        ViewModel.IsFlyoutOpened = false;
    }
}

/// <summary>
/// 位于标题栏的账户控件基类.
/// </summary>
public abstract class HeaderAccountControlBase : LayoutUserControlBase<AccountViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderAccountControlBase"/> class.
    /// </summary>
    protected HeaderAccountControlBase() => ViewModel = this.Get<AccountViewModel>();
}
