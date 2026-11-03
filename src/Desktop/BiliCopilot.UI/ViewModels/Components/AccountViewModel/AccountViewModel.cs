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
    /// 所以只有分类器确证凭据失效、且用户选择重新登录时才走退出登录的流程。
    /// 加载态由本方法统一维护，每次尝试开始置为加载中，成功、放弃或交给
    /// 重新登录流程前收起。
    /// </remarks>
    private async Task LoadProfileAsync()
    {
        while (true)
        {
            this.Get<AppViewModel>().IsInitialLoading = true;
            AccountProfileLoadFailureKind failureKind;
            try
            {
                await ApplyProfileAsync();
                this.Get<AppViewModel>().IsInitialLoading = false;
                return;
            }
            catch (Exception ex)
            {
                failureKind = AccountProfileLoadFailureClassifier.Classify(ex);
                if (failureKind == AccountProfileLoadFailureKind.AuthenticationExpired)
                {
                    _logger.LogCritical(ex, "无法获取登录用户的资料");

                    // 交给重新登录流程前先收起加载态，让对话框出现在稳定界面上.
                    this.Get<AppViewModel>().IsInitialLoading = false;
                }
                else
                {
                    _logger.LogWarning(ex, "获取登录用户的资料时遇到网络错误，等待用户重试");
                }
            }

            var chosePrimaryButton = await ShowProfileLoadFailureDialogAsync(failureKind);
            switch (AccountProfileLoadPolicy.Decide(failureKind, chosePrimaryButton))
            {
                case AccountProfileLoadDecision.Retry:
                    continue;
                case AccountProfileLoadDecision.SignOutAndRestart:
                    await SignOutAsync();
                    return;
                default:
                    // 用户放弃：只按原有托盘退出语义离开，绝不清理登录态.
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
        this.Get<AppViewModel>().CheckUpateCommand.Execute(default);
        UpdateUnreadCommand.Execute(default);
    }

    /// <summary>
    /// 按失败类别展示对应的处理对话框.
    /// </summary>
    /// <param name="failureKind">失败类别.</param>
    /// <returns>用户选择主按钮时返回 <c>true</c>.</returns>
    private Task<bool> ShowProfileLoadFailureDialogAsync(AccountProfileLoadFailureKind failureKind)
        => failureKind == AccountProfileLoadFailureKind.AuthenticationExpired
            ? ShowDialogAsync(
                ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.LoginExpiredTitle),
                ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.LoginExpiredDescription),
                ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.ReSignIn),
                ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Exit))
            : ShowDialogAsync(
                ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.ProfileLoadFailedTitle),
                ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.NetworkError),
                ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Retry),
                ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Exit));

    /// <summary>
    /// 展示一个带主按钮与关闭按钮的对话框.
    /// </summary>
    /// <param name="title">对话框标题.</param>
    /// <param name="content">对话框内容.</param>
    /// <param name="primaryButtonText">主按钮文本.</param>
    /// <param name="closeButtonText">关闭按钮文本.</param>
    /// <returns>用户选择主按钮时返回 <c>true</c>.</returns>
    private async Task<bool> ShowDialogAsync(string title, string content, string primaryButtonText, string closeButtonText)
    {
        var dialog = new ContentDialog()
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Get<AppViewModel>().ActivatedWindow.Content.XamlRoot,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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
