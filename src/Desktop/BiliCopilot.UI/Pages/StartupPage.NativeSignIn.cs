// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using static BiliCopilot.UI.Toolkits.NativeSignInClient;

namespace BiliCopilot.UI.Pages;

public sealed partial class StartupPage
{
    private const string QrLoginMethod = "qr";
    private const string PasswordLoginMethod = "password";
    private const string SmsLoginMethod = "sms";

    private NativeSignInClient? _nativeSignIn;
    private CancellationTokenSource? _nativeSignInLifetime;
    private (string DialingCode, string Phone, string Key, DateTimeOffset SentAt)? _sentCode;
    private bool _nativeSignInBusy;
    private bool _regionsLoaded;

    private void InitializeNativeSignIn()
    {
        _nativeSignIn ??= new NativeSignInClient();
        _nativeSignInLifetime ??= new CancellationTokenSource();
        PhoneRegion.ItemsSource ??= new[] { new PhoneRegion("86", "中国大陆") };
        PhoneRegion.SelectedIndex = 0;
        LoginMethods.SetItems(
            new[]
            {
                CreateLoginTab(QrLoginMethod, StringNames.QrSignInMode),
                CreateLoginTab(PasswordLoginMethod, StringNames.PasswordSignInMode),
                CreateLoginTab(SmsLoginMethod, StringNames.SmsSignInMode),
            },
            QrLoginMethod);
        ShowLoginMethod(QrLoginMethod);
    }

    private void DisposeNativeSignIn(object sender, RoutedEventArgs args)
    {
        ViewModel.ExitCommand.Execute(false);
        _nativeSignInLifetime?.Cancel();
        _nativeSignInLifetime?.Dispose();
        _nativeSignInLifetime = null;
        _nativeSignIn?.Dispose();
        _nativeSignIn = null;
        _sentCode = null;
        _regionsLoaded = false;
        LoginPassword.Password = string.Empty;
        LoginSmsCode.Text = string.Empty;
    }

    private static SelectorBarItem CreateLoginTab(string method, StringNames label)
    {
        return new SelectorBarItem
        {
            Tag = method,
            Text = ResourceToolkit.GetLocalizedString(label),
        };
    }

    private void ShowLoginMethod(string selection)
    {
        var isQr = selection == QrLoginMethod;
        ViewModel.IsQRCodeSignInSelected = isQr;
        QrSignInPanel.Visibility = isQr ? Visibility.Visible : Visibility.Collapsed;
        PasswordSignInPanel.Visibility = selection == PasswordLoginMethod ? Visibility.Visible : Visibility.Collapsed;
        SmsSignInPanel.Visibility = selection == SmsLoginMethod ? Visibility.Visible : Visibility.Collapsed;
        ShowNativeSignInStatus(null);
    }

    private async void OnLoginMethodChanged(object? sender, EventArgs args)
    {
        if (LoginMethods.SelectedItem is not string selection || _nativeSignInLifetime is null)
        {
            return;
        }

        ShowLoginMethod(selection);
        PageTransition.Play(LoginFormViewport);
        if (selection == QrLoginMethod)
        {
            if (ViewModel.QRCodeImage is not null)
            {
                ViewModel.ReloadQRCodeCommand.Execute(default);
            }

            return;
        }

        await ViewModel.ExitCommand.ExecuteAsync(false);
        if (selection == SmsLoginMethod && !_regionsLoaded)
        {
            await RunNativeSignInAsync("加载短信区号", async (client, token) =>
            {
                var regions = await client.GetRegionsAsync(token);
                token.ThrowIfCancellationRequested();
                PhoneRegion.ItemsSource = regions;
                PhoneRegion.SelectedItem = regions.FirstOrDefault(region => region.DialingCode == "86") ?? regions.FirstOrDefault();
                _regionsLoaded = true;
            });
        }
    }

    private async void OnPasswordSubmitAsync(object sender, RoutedEventArgs args)
    {
        var account = LoginAccount.Text.Trim();
        var password = LoginPassword.Password;
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
        {
            ShowNativeSignInStatus("请输入账号和密码。");
            return;
        }

        await RunNativeSignInAsync("密码登录", async (client, token) =>
        {
            LoginPassword.Password = string.Empty;
            var result = await CompletePasswordSignInAsync(client, account, password, token);
            if (result is not null)
            {
                await FinishNativeSignInAsync(result, token);
            }
        });
    }

    private async void OnRequestSmsCodeAsync(object sender, RoutedEventArgs args)
    {
        if (!TryGetPhone(out var dialingCode, out var phone))
        {
            return;
        }

        if (_sentCode is { } previous && previous.DialingCode == dialingCode && previous.Phone == phone
            && DateTimeOffset.UtcNow - previous.SentAt < TimeSpan.FromMinutes(1))
        {
            ShowNativeSignInStatus("验证码已发送，请等 60 秒后再试。");
            return;
        }

        await RunNativeSignInAsync("发送登录短信", async (client, token) =>
        {
            var key = await SendSmsWithCaptchaAsync(client, dialingCode, phone, token);
            if (key is null)
            {
                return;
            }

            _sentCode = (dialingCode, phone, key, DateTimeOffset.UtcNow);
            ShowNativeSignInStatus("验证码已发送。");
            LoginSmsCode.Focus(FocusState.Programmatic);
        });
    }

    private async void OnSmsSubmitAsync(object sender, RoutedEventArgs args)
    {
        if (!TryGetPhone(out var dialingCode, out var phone))
        {
            return;
        }

        if (_sentCode is not { } sent || sent.DialingCode != dialingCode || sent.Phone != phone
            || DateTimeOffset.UtcNow - sent.SentAt >= TimeSpan.FromMinutes(5))
        {
            ShowNativeSignInStatus("请先为当前手机号获取验证码。");
            return;
        }

        var code = LoginSmsCode.Text.Trim();
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
        {
            ShowNativeSignInStatus("请输入六位短信验证码。");
            return;
        }

        await RunNativeSignInAsync("短信登录", async (client, token) =>
        {
            var result = await client.SignInWithSmsAsync(dialingCode, phone, code, sent.Key, token);
            LoginSmsCode.Text = string.Empty;
            await FinishNativeSignInAsync(result, token);
        });
    }

    private bool TryGetPhone(out string dialingCode, out string phone)
    {
        if (PhoneRegion.SelectedItem is PhoneRegion region && region.TryNormalizePhone(LoginPhone.Text, out phone))
        {
            dialingCode = region.DialingCode;
            return true;
        }

        dialingCode = phone = string.Empty;
        ShowNativeSignInStatus("请选择国家或地区并输入有效的手机号。");
        return false;
    }

    private async Task RunNativeSignInAsync(string stage, Func<NativeSignInClient, CancellationToken, Task> action)
    {
        if (_nativeSignInBusy || _nativeSignIn is not { } client || _nativeSignInLifetime is not { } lifetime)
        {
            return;
        }

        var token = lifetime.Token;
        _nativeSignInBusy = true;
        LoginMethods.IsEnabled = false;
        PasswordSignInPanel.IsHitTestVisible = false;
        SmsSignInPanel.IsHitTestVisible = false;
        PasswordSubmit.IsEnabled = SmsSubmit.IsEnabled = RequestSmsCode.IsEnabled = false;
        NativeSignInProgress.Visibility = Visibility.Visible;
        ShowNativeSignInStatus(null);
        try
        {
            await action(client, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (SignInException ex) when (!token.IsCancellationRequested)
        {
            SignInDiagnostics.LogFailure(this.Get<ILogger<StartupPage>>(), stage, ex);
            ShowNativeSignInStatus(ex.Message);
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                SignInDiagnostics.LogFailure(this.Get<ILogger<StartupPage>>(), stage, ex);
                ShowNativeSignInStatus(ResourceToolkit.GetLocalizedString(StringNames.LoginFailed));
            }
        }
        finally
        {
            _nativeSignInBusy = false;
            LoginMethods.IsEnabled = true;
            PasswordSignInPanel.IsHitTestVisible = true;
            SmsSignInPanel.IsHitTestVisible = true;
            PasswordSubmit.IsEnabled = SmsSubmit.IsEnabled = RequestSmsCode.IsEnabled = true;
            NativeSignInProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowNativeSignInStatus(string? text)
    {
        NativeSignInStatus.Text = text ?? string.Empty;
        NativeSignInStatus.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task FinishNativeSignInAsync(AppSignInResult result, CancellationToken cancellationToken)
    {
        var expectedUid = ViewModel.SignInWindow?.ExpectedUid;
        await GlobalDependencies.Accounts.SignInAsync(result, expectedUid, cancellationToken, activateOnSuccess: expectedUid.HasValue);
        cancellationToken.ThrowIfCancellationRequested();
        await ViewModel.ExitCommand.ExecuteAsync(true);
    }

    private async Task<CaptchaProof?> ShowCaptchaAsync(CaptchaChallenge challenge, CancellationToken cancellationToken)
    {
        const string captchaPage = "https://captcha.bilicopilot.invalid/";
        const double viewportWidth = 288;
        const double viewportHeight = 344;
        var darkMode = ActualTheme == ElementTheme.Dark;
        var webView = new WebView2
        {
            Width = viewportWidth,
            Height = viewportHeight,
            DefaultBackgroundColor = Microsoft.UI.Colors.Transparent,
            IsHitTestVisible = false,
            IsTabStop = false,
        };
        var loading = new ProgressRing
        {
            Width = 28,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsActive = true,
        };
        var viewport = new Canvas { Width = viewportWidth, Height = viewportHeight };
        viewport.Children.Add(webView);
        var challengeView = new Grid();
        challengeView.Children.Add(viewport);
        challengeView.Children.Add(loading);

        var refreshLabel = ResourceToolkit.GetLocalizedString(StringNames.Refresh);
        var refresh = new Button
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed,
            Content = new FontIcon
            {
                FontFamily = (FontFamily)Application.Current.Resources["BiliIconsFontFamily"],
                Glyph = (string)Application.Current.Resources["IconArrowSync"],
                FontSize = 16,
            },
        };
        AutomationProperties.SetName(refresh, refreshLabel);
        AutomationProperties.SetAutomationId(refresh, "CaptchaRefresh");
        ToolTipService.SetToolTip(refresh, refreshLabel);
        var cancel = new Button { Content = ResourceToolkit.GetLocalizedString(StringNames.Cancel), MinWidth = 72 };
        AutomationProperties.SetAutomationId(cancel, "CaptchaCancel");
        var confirm = new Button
        {
            Content = ResourceToolkit.GetLocalizedString(StringNames.Confirm),
            MinWidth = 72,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            IsEnabled = false,
            Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetAutomationId(confirm, "CaptchaConfirm");
        var trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        trailing.Children.Add(cancel);
        trailing.Children.Add(confirm);
        var footer = new Grid();
        footer.Children.Add(refresh);
        footer.Children.Add(trailing);
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(challengeView);
        content.Children.Add(footer);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Content = content,
        };
        AutomationProperties.SetName(dialog, ResourceToolkit.GetLocalizedString(StringNames.NativeSignInCaptcha));
        dialog.Resources["ContentDialogPadding"] = new Thickness(16, 8, 16, 12);
        dialog.Resources["ContentDialogTopOverlay"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        confirm.Click += (_, _) => webView.CoreWebView2.PostWebMessageAsString("confirm");
        refresh.Click += (_, _) => webView.CoreWebView2.PostWebMessageAsString("refresh");
        cancel.Click += (_, _) => dialog.Hide();
        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, args) =>
        {
            args.Handled = true;
            dialog.Hide();
        };
        dialog.KeyboardAccelerators.Add(escape);

        var messageKey = Guid.NewGuid().ToString("N");
        using var page = new MemoryStream(Encoding.UTF8.GetBytes(CreateCaptchaHtml(challenge, messageKey, darkMode))).AsRandomAccessStream();
        var completion = new TaskCompletionSource<CaptchaProof?>(TaskCreationOptions.RunContinuationsAsynchronously);
        webView.WebMessageReceived += (_, args) =>
        {
            if (completion.Task.IsCompleted || !string.Equals(args.Source, captchaPage, StringComparison.Ordinal))
            {
                return;
            }

            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            var message = document.RootElement;
            if (message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("key", out var key)
                || key.ValueKind != JsonValueKind.String || key.GetString() != messageKey)
            {
                return;
            }

            if (message.TryGetProperty("failed", out var failure) && failure.ValueKind == JsonValueKind.True)
            {
                completion.TrySetException(new SignInException("安全验证加载失败，请重试或使用网页登录。"));
            }
            else if (TryReadProof(message, out var result))
            {
                completion.TrySetResult(result);
            }
            else if (message.TryGetProperty("closed", out var closed) && closed.ValueKind == JsonValueKind.True)
            {
                completion.TrySetResult(null);
            }
            else if (message.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True)
            {
                loading.IsActive = false;
                loading.Visibility = Visibility.Collapsed;
                webView.IsHitTestVisible = true;
                webView.IsTabStop = true;
                webView.Focus(FocusState.Programmatic);
            }
            else if (message.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Object)
            {
                confirm.IsEnabled = ReadFlag(actions, "canConfirm");
                confirm.Visibility = ReadFlag(actions, "confirm") ? Visibility.Visible : Visibility.Collapsed;
                refresh.Visibility = ReadFlag(actions, "refresh") ? Visibility.Visible : Visibility.Collapsed;
                var height = actions.TryGetProperty("visibleHeight", out var visibleHeight)
                    && visibleHeight.ValueKind == JsonValueKind.Number
                    && visibleHeight.TryGetDouble(out var measuredHeight)
                    && measuredHeight is >= 230 and < viewportHeight
                    ? measuredHeight
                    : viewportHeight;
                viewport.Height = height;
                viewport.Clip = height < viewportHeight
                    ? new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, viewportWidth, height) }
                    : null;
            }
        };
        webView.Loaded += async (_, _) =>
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                if (completion.Task.IsCompleted)
                {
                    return;
                }

                var browser = webView.CoreWebView2;
                browser.Settings.AreHostObjectsAllowed = false;
                browser.Settings.AreDevToolsEnabled = false;
                browser.NavigationStarting += (_, args) => args.Cancel = !string.Equals(args.Uri, captchaPage, StringComparison.Ordinal);
                browser.NewWindowRequested += (_, args) => args.Handled = true;
                browser.AddWebResourceRequestedFilter(captchaPage, CoreWebView2WebResourceContext.Document, CoreWebView2WebResourceRequestSourceKinds.Document);
                browser.WebResourceRequested += (sender, args) =>
                {
                    if (string.Equals(args.Request.Uri, captchaPage, StringComparison.Ordinal))
                    {
                        page.Seek(0);
                        args.Response = sender.Environment.CreateWebResourceResponse(page, 200, "OK", "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store");
                    }
                };
                browser.Navigate(captchaPage);
            }
            catch (Exception)
            {
                completion.TrySetException(new SignInException("安全验证加载失败，请重试或使用网页登录。"));
            }
        };

        using var cancellation = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        try
        {
            var dialogTask = dialog.ShowAsync().AsTask();
            if (await Task.WhenAny(completion.Task, dialogTask) == completion.Task && !dialogTask.IsCompleted)
            {
                dialog.Hide();
            }

            await dialogTask;
            completion.TrySetResult(null);
            cancellationToken.ThrowIfCancellationRequested();
            return await completion.Task;
        }
        finally
        {
            viewport.Children.Remove(webView);
            webView.Close();
        }
    }

    private static bool ReadFlag(JsonElement message, string name)
        => message.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryReadProof(JsonElement message, out CaptchaProof? proof)
    {
        proof = null;
        if (!TryReadString(message, "challenge", out var challenge)
            || !TryReadString(message, "validate", out var validate)
            || !TryReadString(message, "seccode", out var seccode))
        {
            return false;
        }

        proof = new(challenge, validate, seccode);
        return true;
    }

    private static bool TryReadString(JsonElement message, string name, out string value)
    {
        value = message.TryGetProperty(name, out var node) && node.ValueKind == JsonValueKind.String
            ? node.GetString() ?? string.Empty
            : string.Empty;
        return value.Length is > 0 and <= 512;
    }

    private static string CreateCaptchaHtml(CaptchaChallenge challenge, string key, bool darkMode)
        => $$"""
            <!doctype html>
            <html lang="zh-Hans">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta name="color-scheme" content="{{(darkMode ? "dark" : "light")}}">
              <meta http-equiv="Content-Security-Policy"
                    content="default-src 'none'; script-src 'nonce-{{key}}' https://*.geetest.com; style-src 'unsafe-inline' https://*.geetest.com; img-src data: https://*.geetest.com; connect-src https://*.geetest.com; frame-src https://*.geetest.com">
              <style>
                :root, body { margin: 0; background: transparent; }
                .geetest_fullpage_ghost, .geetest_panel_ghost, .geetest_popup_ghost {
                  opacity: 0 !important;
                }
                .geetest_wind.geetest_fullpage_click .geetest_fullpage_click_box,
                .geetest_holder.geetest_silver .geetest_widget,
                .geetest_holder.geetest_silver.geetest_popup .geetest_popup_box {
                  background: transparent !important;
                  border-color: transparent !important;
                  box-shadow: none !important;
                }
                [data-native-action], [data-native-only] { display: none !important; }
              </style>
            </head>
            <body>
              <script nonce="{{key}}">
                const key = {{JsonSerializer.Serialize(key)}};
                const fail = () => chrome.webview.postMessage({ key, failed: true });
                const close = () => chrome.webview.postMessage({ key, closed: true });
                const startupTimeout = setTimeout(fail, 15000);
                let buttons = {};
                let lastActions;
                const canClick = button => button && !button.classList.contains("geetest_disable")
                  && !button.disabled && button.getAttribute("aria-disabled") !== "true";
                const syncActions = () => {
                  const toolbar = [...document.querySelectorAll(".geetest_holder.geetest_silver .geetest_panel")]
                    .find(panel => panel.closest(".geetest_silver").getClientRects().length);
                  const confirm = toolbar?.querySelector(".geetest_commit");
                  const refresh = toolbar?.querySelector(".geetest_refresh");
                  buttons = { confirm, refresh };
                  confirm?.setAttribute("data-native-action", "");
                  refresh?.setAttribute("data-native-action", "");
                  const compact = Boolean((confirm || refresh) && ![...toolbar.querySelectorAll(
                    "a.geetest_close, a.geetest_feedback, a.geetest_voice"
                  )].some(button => getComputedStyle(button).display !== "none"));
                  toolbar?.toggleAttribute("data-native-only", compact);
                  const picture = toolbar?.closest(".geetest_silver")?.querySelector(".geetest_table_box");
                  const actions = {
                    confirm: Boolean(confirm),
                    refresh: Boolean(refresh),
                    canConfirm: Boolean(canClick(confirm)),
                    visibleHeight: compact && picture ? Math.ceil(picture.getBoundingClientRect().bottom) + 4 : null
                  };
                  const state = JSON.stringify(actions);
                  if (lastActions !== state) {
                    lastActions = state;
                    chrome.webview.postMessage({ key, actions });
                  }
                };
                chrome.webview.addEventListener("message", ({ data }) => {
                  if (data !== "confirm" && data !== "refresh") return;
                  const button = buttons[data];
                  if (canClick(button)) button.click();
                });
                const script = document.createElement("script");
                script.src = "https://static.geetest.com/static/tools/gt.js";
                script.onerror = fail;
                script.onload = () => {
                  try {
                    initGeetest({
                      gt: {{JsonSerializer.Serialize(challenge.SiteKey)}},
                      challenge: {{JsonSerializer.Serialize(challenge.Challenge)}},
                      offline: false,
                      new_captcha: true,
                      product: "bind",
                      next_width: "260px",
                      bg_color: "transparent",
                      hideClose: true,
                      hideSuccess: true,
                      timeout: 15000,
                      https: true
                    }, captcha => {
                      captcha.onError(fail);
                      captcha.onClose(close);
                      captcha.onSuccess(() => {
                        const result = captcha.getValidate();
                        chrome.webview.postMessage({
                          key,
                          challenge: result.geetest_challenge,
                          validate: result.geetest_validate,
                          seccode: result.geetest_seccode
                        });
                      });
                      captcha.onReady(() => {
                        clearTimeout(startupTimeout);
                        new MutationObserver(syncActions).observe(document.body, {
                          subtree: true, childList: true, attributes: true,
                          attributeFilter: ["class", "aria-disabled", "disabled", "style", "hidden"]
                        });
                        document.addEventListener("keydown", event => {
                          if (event.key === "Escape") {
                            close();
                          }
                        });
                        chrome.webview.postMessage({ key, ready: true });
                        try {
                          captcha.verify();
                        } catch {
                          fail();
                        }
                      });
                    });
                  } catch {
                    fail();
                  }
                };
                document.head.append(script);
              </script>
            </body>
            </html>
            """;
}
