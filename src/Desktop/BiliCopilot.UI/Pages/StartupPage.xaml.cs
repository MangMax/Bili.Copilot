// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Navigation;
using Richasy.WinUIKernel.Share.Base;
using Windows.System;

namespace BiliCopilot.UI.Pages;

/// <summary>
/// 初始登录页面.
/// </summary>
public sealed partial class StartupPage : StartupPageBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartupPage"/> class.
    /// </summary>
    public StartupPage()
    {
        InitializeComponent();
        Unloaded += DisposeNativeSignIn;
    }

    /// <inheritdoc/>
    protected override void OnPageLoaded()
    {
        InitializeNativeSignIn();
        ViewModel.InitializeCommand.Execute(QRCodeImage);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SignInWindow = e.Parameter as StartupWindow;
    }

    private void OnWebSignInButtonClick(object sender, RoutedEventArgs e)
    {
        var window = this.Get<AppViewModel>().Windows.OfType<WebSignInWindow>()
            .FirstOrDefault(window => window.Startup == ViewModel);
        if (window is not null)
        {
            window.Activate();
        }
        else
        {
            new WebSignInWindow(ViewModel, ViewModel.SignInWindow?.ExpectedUid).Activate();
        }
    }

    private async void OnRepoButtonClickAsync(object sender, RoutedEventArgs e)
        => await Launcher.LaunchUriAsync(new Uri("https://github.com/MangMax/Bili.Copilot"));

    private async void OnBiliButtonClickAsync(object sender, RoutedEventArgs e)
        => await Launcher.LaunchUriAsync(new Uri("https://space.bilibili.com/5992670"));

    private void OnRefreshQRButtonClick(object sender, RoutedEventArgs e)
        => ViewModel.ReloadQRCodeCommand.Execute(default);
}

/// <summary>
/// 初始登录页面基类.
/// </summary>
public abstract class StartupPageBase : LayoutPageBase<StartupPageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartupPageBase"/> class.
    /// </summary>
    public StartupPageBase()
        => ViewModel = new StartupPageViewModel(this.Get<ILogger<StartupPageViewModel>>(), this.Get<Microsoft.UI.Dispatching.DispatcherQueue>());
}
