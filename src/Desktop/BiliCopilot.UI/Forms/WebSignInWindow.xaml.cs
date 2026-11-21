// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Richasy.WinUIKernel.Share.Base;
using WinUIEx;

namespace BiliCopilot.UI.Forms;

/// <summary>
/// 网页登录窗口.
/// </summary>
public sealed partial class WebSignInWindow : WindowBase
{
    private static readonly Uri SignInUri = new("https://passport.bilibili.com/login");
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isImporting;
    private readonly long? _expectedUid;
    private bool _completed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSignInWindow"/> class.
    /// </summary>
    public WebSignInWindow()
        : this(null, GlobalDependencies.Accounts.CurrentUid)
    {
    }

    internal WebSignInWindow(StartupPageViewModel? startup, long? expectedUid)
    {
        Startup = startup;
        _expectedUid = expectedUid;
        InitializeComponent();
        Width = 1200;
        Height = 632;

        Title = ResourceToolkit.GetLocalizedString(StringNames.PasswordOrSmsSignIn);
        TitleBar.Title = Title;

        this.CenterOnScreen();
        this.SetIcon("Assets/logo.ico");
        this.SetTitleBar(TitleBar);
        this.Get<AppViewModel>().Windows.Add(this);

        Closed += OnClosed;
        Activated += OnActivated;
    }

    internal StartupPageViewModel? Startup { get; }

    internal long? ExpectedUid => _expectedUid;

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            Activated -= OnActivated;
            Startup?.ExitCommand.Execute(false);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        Activated -= OnActivated;
        Closed -= OnClosed;
        var app = this.Get<AppViewModel>();
        app.Windows.Remove(this);
        MainView.Close();
        if (_completed)
        {
            Startup?.ExitCommand.Execute(true);
        }
        else if (!app.IsClosed && Startup is { IsQRCodeSignInSelected: true, QRCodeImage.IsLoaded: true } startup)
        {
            startup.ReloadQRCodeCommand.Execute(default);
        }
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = Microsoft.Windows.Storage.ApplicationData.GetDefault().TemporaryFolder.Path;
            var profile = Path.Combine(root, "AccountLogin", Guid.NewGuid().ToString("N"));
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, profile, null);
            environment.BrowserProcessExited += (_, _) =>
            {
                try
                {
                    Directory.Delete(profile, recursive: true);
                }
                catch (IOException)
                {
                    // The package temporary folder can be reclaimed by Windows.
                }
                catch (UnauthorizedAccessException)
                {
                }
            };
            await MainView.EnsureCoreWebView2Async(environment);
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                MainView.CoreWebView2.Navigate(SignInUri.AbsoluteUri);
            }
        }
        catch (Exception ex)
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                SignInDiagnostics.LogFailure(this.Get<ILogger<WebSignInWindow>>(), "初始化网页登录窗口", ex);
                SignInError.IsOpen = true;
            }
        }
    }

    private async void OnNavigationCompletedAsync(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
    {
        if (_isImporting || _cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        if (!args.IsSuccess)
        {
            this.Get<ILogger<WebSignInWindow>>().LogWarning("网页登录页面加载失败，WebView 状态 {Status}", args.WebErrorStatus);
            SignInError.IsOpen = true;
            return;
        }

        if (sender.Source is not { Scheme: "https", Host: "www.bilibili.com" })
        {
            return;
        }

        _isImporting = true;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var cookies = await sender.CoreWebView2.CookieManager.GetCookiesAsync("https://www.bilibili.com/");
            var cookieDict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var cookie in cookies)
            {
                cookieDict[cookie.Name] = cookie.Value;
            }

            if (!cookieDict.TryGetValue("SESSDATA", out var session) || string.IsNullOrWhiteSpace(session))
            {
                sender.CoreWebView2.Navigate(SignInUri.AbsoluteUri);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await GlobalDependencies.Accounts.SignInAsync(
                new Richasy.BiliKernel.Bili.AuthorizeExecutionSettings { Cookies = cookieDict },
                _expectedUid, cancellationToken, activateOnSuccess: Startup?.SignInWindow?.ExpectedUid.HasValue == true);
            cancellationToken.ThrowIfCancellationRequested();
            _completed = true;
            Close();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SignInDiagnostics.LogFailure(this.Get<ILogger<WebSignInWindow>>(), "导入网页登录结果", ex);
                SignInError.IsOpen = true;
                sender.CoreWebView2.Navigate(SignInUri.AbsoluteUri);
            }
        }
        finally
        {
            _isImporting = false;
        }
    }
}
