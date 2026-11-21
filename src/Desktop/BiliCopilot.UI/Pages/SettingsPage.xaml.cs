// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.UI.Xaml.Input;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Pages;

/// <summary>
/// 设置页面.
/// </summary>
public sealed partial class SettingsPage : SettingsPageBase, IParameterPage, IInitializePage, ICancelPageViewModel
{
    private SettingsSection _currentSection = SettingsSection.Root;
    private SettingsSection? _pendingSection;
    private bool _isPageLoaded;
    private bool _isLoadingChatControls;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsPage"/> class.
    /// </summary>
    public SettingsPage() => InitializeComponent();

    private AppUpdateViewModel Update { get; } = GlobalDependencies.Kernel.GetRequiredService<AppUpdateViewModel>();

    /// <inheritdoc/>
    public void SetParameter(object? parameter)
    {
        var target = SettingsNavigationPolicy.ResolveSection(parameter);
        _pendingSection = target;

        if (_isPageLoaded && Visibility == Visibility.Visible)
        {
            _pendingSection = null;
            _ = ShowSectionAsync(target, animate: true);
        }
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        ViewModel.InitializeCommand.Execute(default);
        if (_isPageLoaded)
        {
            var target = _pendingSection ?? SettingsSection.Root;
            _pendingSection = null;
            _ = ShowSectionAsync(target, animate: false);
        }
    }

    /// <inheritdoc/>
    public void CancelLoading()
    {
        this.Get<NavigationViewModel>().IsSettingsSubpageOpen = false;

        if (SettingsNavigationPolicy.TryGoBack(_currentSection, out var target))
        {
            _ = ShowSectionAsync(target, animate: Visibility == Visibility.Visible);
        }
    }

    /// <inheritdoc/>
    protected override void OnPageLoaded()
    {
        ViewModel.InitializeCommand.Execute(default);
        _isPageLoaded = true;
        var target = _pendingSection ?? SettingsSection.Root;
        _pendingSection = null;
        _ = ShowSectionAsync(target, animate: false);
    }

    private void OnSectionCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: var tag })
        {
            var target = SettingsNavigationPolicy.ResolveSection(tag);
            _ = ShowSectionAsync(target, animate: true);
        }
    }

    private void OnBackButtonClick(object sender, RoutedEventArgs e)
        => TryNavigateBack();

    private void OnBackAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (TryNavigateBack())
        {
            args.Handled = true;
        }
    }

    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsXButton1Pressed && TryNavigateBack())
        {
            e.Handled = true;
        }
    }

    internal bool TryNavigateBack()
    {
        if (!SettingsNavigationPolicy.TryGoBack(_currentSection, out var target))
        {
            return false;
        }

        _ = ShowSectionAsync(target, animate: true);
        return true;
    }

    private async Task ShowSectionAsync(SettingsSection target, bool animate)
    {
        var previous = _currentSection;
        if (animate)
        {
            var (previousIndex, currentIndex) = SettingsNavigationPolicy.GetTransitionIndices(previous, target);
            if (previousIndex >= 0 && currentIndex >= 0)
            {
                PageTransitionSession.Publish(PageTransitionKind.Tab, previousIndex, currentIndex, DateTimeOffset.Now);
            }
        }
        else
        {
            PageTransitionSession.Clear();
        }

        _currentSection = target;
        this.Get<NavigationViewModel>().IsSettingsSubpageOpen = SettingsNavigationPolicy.IsSubpage(target);
        UpdateHeader(target);
        UpdateContainerVisibility(target);

        var targetContainer = GetContainer(target);
        if (previous != target)
        {
            targetContainer.ScrollTo(0, 0);
        }

        if (SettingsNavigationPolicy.ShouldSaveAIServicesOnLeave(previous, target))
        {
            await ViewModel.CheckSaveServicesAsync();
        }

        if (SettingsNavigationPolicy.ShouldRefreshCacheSizeOnEnter(target))
        {
            ViewModel.InitializeCommand.Execute(default);
        }

        if (SettingsNavigationPolicy.ShouldInitializeAIServicesOnEnter(target))
        {
            await EnsureChatControlsLoadedAsync();
        }
    }

    private void UpdateHeader(SettingsSection section)
    {
        var isSubpage = SettingsNavigationPolicy.IsSubpage(section);
        RootTitleBlock.Visibility = isSubpage ? Visibility.Collapsed : Visibility.Visible;
        SubpageHeaderPanel.Visibility = isSubpage ? Visibility.Visible : Visibility.Collapsed;
        SubpageTitleBlock.Text = isSubpage ? GetSectionTitle(section) : string.Empty;
    }

    private void UpdateContainerVisibility(SettingsSection target)
    {
        RootContainer.Visibility = target == SettingsSection.Root ? Visibility.Visible : Visibility.Collapsed;
        PlaybackContainer.Visibility = target == SettingsSection.Playback ? Visibility.Visible : Visibility.Collapsed;
        AppearanceNavigationContainer.Visibility = target == SettingsSection.AppearanceAndNavigation ? Visibility.Visible : Visibility.Collapsed;
        StorageDownloadContainer.Visibility = target == SettingsSection.StorageAndDownload ? Visibility.Visible : Visibility.Collapsed;
        SystemNotificationContainer.Visibility = target == SettingsSection.SystemAndNotification ? Visibility.Visible : Visibility.Collapsed;
        UpdateContainer.Visibility = target == SettingsSection.Update ? Visibility.Visible : Visibility.Collapsed;
        AIContainer.Visibility = target == SettingsSection.AIService ? Visibility.Visible : Visibility.Collapsed;
    }

    private ScrollView GetContainer(SettingsSection section)
        => section switch
        {
            SettingsSection.Playback => PlaybackContainer,
            SettingsSection.AppearanceAndNavigation => AppearanceNavigationContainer,
            SettingsSection.StorageAndDownload => StorageDownloadContainer,
            SettingsSection.SystemAndNotification => SystemNotificationContainer,
            SettingsSection.Update => UpdateContainer,
            SettingsSection.AIService => AIContainer,
            _ => RootContainer,
        };

    private static string GetSectionTitle(SettingsSection section)
        => section switch
        {
            SettingsSection.Playback => ResourceToolkit.GetLocalizedString(StringNames.SettingsCategoryPlayback),
            SettingsSection.AppearanceAndNavigation => ResourceToolkit.GetLocalizedString(StringNames.SettingsCategoryAppearanceNavigation),
            SettingsSection.StorageAndDownload => ResourceToolkit.GetLocalizedString(StringNames.SettingsCategoryStorageDownload),
            SettingsSection.SystemAndNotification => ResourceToolkit.GetLocalizedString(StringNames.SettingsCategorySystemNotification),
            SettingsSection.Update => ResourceToolkit.GetLocalizedString(StringNames.SettingsCategoryUpdate),
            SettingsSection.AIService => ResourceToolkit.GetLocalizedString(StringNames.SettingsCategoryAIService),
            _ => ResourceToolkit.GetLocalizedString(StringNames.Settings),
        };

    private async Task EnsureChatControlsLoadedAsync()
    {
        if (_isLoadingChatControls)
        {
            return;
        }

        _isLoadingChatControls = true;
        try
        {
            await ViewModel.InitializeChatServicesAsync();
            if (AIPanel.Children.Count == 0)
            {
                await LoadChatControlsAsync();
            }
        }
        finally
        {
            _isLoadingChatControls = false;
        }
    }

    private async Task LoadChatControlsAsync()
    {
        foreach (var vm in ViewModel.ChatServices)
        {
            var control = vm.GetSettingControl();

            if (control != null)
            {
                await vm.InitializeCommand.ExecuteAsync(default);
                AIPanel.Children.Add(control);
            }
        }
    }
}

/// <summary>
/// 设置页面基类.
/// </summary>
public abstract class SettingsPageBase : LayoutPageBase<SettingsPageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsPageBase"/> class.
    /// </summary>
    protected SettingsPageBase() => ViewModel = this.Get<SettingsPageViewModel>();
}
