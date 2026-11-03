// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Pages;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.User;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.System;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 账户视图模型.
/// </summary>
public sealed partial class AccountViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccountViewModel"/> class.
    /// </summary>
    public AccountViewModel(
        IMyProfileService myProfileService,
        IMessageService messageService,
        NavigationViewModel navService,
        ILogger<AccountViewModel> logger)
    {
        _navService = navService;
        _messageService = messageService;
        _myProfileService = myProfileService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (MyProfile is not null)
        {
            return;
        }

        IsInitializing = true;
        try
        {
            await LoadProfileAsync();
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>
    /// 加载当前登录用户的资料。网络故障只提示重试，登录态失效才引导重新登录.
    /// </summary>
    /// <remarks>
    /// 资料加载失败可能只是网络抖动，把它当成登录失效会误清登录态，
    /// 所以只有分类器确证凭据失效时才走重新登录的流程。
    /// </remarks>
    private async Task LoadProfileAsync()
    {
        while (true)
        {
            this.Get<AppViewModel>().IsInitialLoading = true;
            try
            {
                await ApplyProfileAsync();
                return;
            }
            catch (Exception ex) when (AccountProfileLoadFailureClassifier.Classify(ex) == AccountProfileLoadFailureKind.AuthenticationExpired)
            {
                _logger.LogCritical(ex, "无法获取登录用户的资料");
                await HandleAuthenticationExpiredAsync();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取登录用户的资料时遇到网络错误，等待用户重试");
                if (await ShouldRetryProfileLoadAsync())
                {
                    continue;
                }

                // 用户放弃重试：只按原有托盘退出语义离开，绝不清理登录态.
                this.Get<AppViewModel>().IsInitialLoading = false;
                await ExitApplicationAsync();
                return;
            }
        }
    }

    /// <summary>
    /// 拉取资料并更新与登录用户相关的界面状态.
    /// </summary>
    private async Task ApplyProfileAsync()
    {
        MyProfile = await _myProfileService.GetMyProfileAsync();
        Introduce = string.IsNullOrEmpty(MyProfile.Introduce)
            ? ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.NoSelfIntroduce)
            : MyProfile.Introduce;
        this.Get<AppViewModel>().IsInitialLoading = false;
        this.Get<AppViewModel>().CheckUpateCommand.Execute(default);
        UpdateUnreadCommand.Execute(default);
    }

    /// <summary>
    /// 提示登录态已失效，并按用户选择重新登录或退出.
    /// </summary>
    private async Task HandleAuthenticationExpiredAsync()
    {
        // 用户可能更改了密码，提醒用户是否需要重新登录.
        var dialog = new ContentDialog()
        {
            Title = ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.LoginExpiredTitle),
            Content = ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.LoginExpiredDescription),
            PrimaryButtonText = ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.ReSignIn),
            CloseButtonText = ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Exit),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Get<AppViewModel>().ActivatedWindow.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await SignOutAsync();
            return;
        }

        await ExitApplicationAsync();
    }

    /// <summary>
    /// 询问用户是否重试加载资料.
    /// </summary>
    /// <returns>用户选择重试时返回 <c>true</c>.</returns>
    private async Task<bool> ShouldRetryProfileLoadAsync()
    {
        var dialog = new ContentDialog()
        {
            Title = ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.ProfileLoadFailedTitle),
            Content = ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.NetworkError),
            PrimaryButtonText = ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Retry),
            CloseButtonText = ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Exit),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Get<AppViewModel>().ActivatedWindow.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    /// <summary>
    /// 按托盘感知的退出语义离开应用.
    /// </summary>
    private async Task ExitApplicationAsync()
    {
        if (this.Get<AppViewModel>().Windows.OfType<MainWindow>().FirstOrDefault() is { } mainWindow)
        {
            // Share the tray-aware exit so players and tray resources are cleaned up.
            await mainWindow.RequestExitAsync();
        }
        else
        {
            App.Current.Exit();
        }
    }

    [RelayCommand]
    private async Task UpdateCommunityInformationAsync()
    {
        if (MyProfile is null)
        {
            return;
        }

        try
        {
            var communityInformation = await _myProfileService.GetMyCommunityInformationAsync();
            MomentCount = communityInformation.MomentCount ?? 0;
            FollowCount = communityInformation.FollowCount ?? 0;
            FansCount = communityInformation.FansCount ?? 0;
            UpdateUnreadCommand.Execute(default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "已登录用户的社区信息获取失败");
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await this.Get<IAuthenticationService>().SignOutAsync().ConfigureAwait(false);
        MyProfile = null;
        this.Get<AppViewModel>().RestartCommand.Execute(default);
    }

    [RelayCommand]
    private async Task OpenPersonalWebsiteAsync()
    {
        if (MyProfile is null)
        {
            return;
        }

        await Launcher.LaunchUriAsync(new Uri($"https://space.bilibili.com/{MyProfile.User.Id}"));
    }

    [RelayCommand]
    private void ShowFans()
        => _navService.NavigateToOver(typeof(FansPage), MyProfile.User.Id);

    [RelayCommand]
    private void ShowFollows()
        => _navService.NavigateToOver(typeof(FollowsPage), MyProfile.User.Id);

    [RelayCommand]
    private void ShowMoments()
        => _navService.NavigateToOver(typeof(UserSpacePage), MyProfile.User);

    [RelayCommand]
    private void ShowViewLater()
        => _navService.NavigateToOver(typeof(ViewLaterPage));

    [RelayCommand]
    private void ShowHistory()
        => _navService.NavigateToOver(typeof(HistoryPage));

    [RelayCommand]
    private void ShowFavorites()
        => _navService.NavigateToOver(typeof(FavoritesPage));

    [RelayCommand]
    private async Task UpdateUnreadAsync()
    {
        try
        {
            var unreadInfo = await _messageService.GetUnreadInformationAsync();
            _navService.FooterItems.First(p => p.PageKey == typeof(MessagePage).FullName).ShowUnread = unreadInfo.Total > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未读消息获取失败");
        }
    }
}
