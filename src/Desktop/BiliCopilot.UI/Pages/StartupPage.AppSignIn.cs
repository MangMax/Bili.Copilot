// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using static BiliCopilot.UI.Toolkits.NativeSignInClient;

namespace BiliCopilot.UI.Pages;

public sealed partial class StartupPage
{
    private async Task<string?> SendSmsWithCaptchaAsync(NativeSignInClient client, string dialingCode, string phone, CancellationToken token)
    {
        var challenge = await client.GetCaptchaAsync(token);
        while (await ShowCaptchaAsync(challenge, token) is { } proof)
        {
            try
            {
                return await client.SendCodeAsync(dialingCode, phone, challenge, proof, token);
            }
            catch (SignInException ex) when (ex.Captcha is { } nextChallenge)
            {
                challenge = nextChallenge;
            }
        }

        return null;
    }

    private async Task<AppSignInResult?> CompletePasswordSignInAsync(NativeSignInClient client, string account, string password, CancellationToken token)
    {
        CaptchaChallenge? challenge = null;
        CaptchaProof? proof = null;
        while (true)
        {
            try
            {
                return await client.SignInWithPasswordAsync(account, password, challenge, proof, token);
            }
            catch (SignInException ex) when (ex.Captcha is { } nextChallenge)
            {
                challenge = nextChallenge;
                proof = await ShowCaptchaAsync(challenge, token);
                if (proof is null)
                {
                    return null;
                }
            }
            catch (SignInException ex) when (ex.PhoneVerification is { } verification)
            {
                return await CompletePhoneVerificationAsync(client, verification, token);
            }
        }
    }

    private async Task<AppSignInResult?> CompletePhoneVerificationAsync(NativeSignInClient client, PhoneVerification verification, CancellationToken token)
    {
        var phone = await client.GetVerificationPhoneAsync(verification, token);
        var send = CreatePhoneVerificationDialog("发送验证码");
        send.Content = phone is null ? "B 站要求验证此账号绑定的手机号。" : $"B 站要求验证此账号绑定的手机号 {phone}。";
        if (await ShowPhoneVerificationDialogAsync(send, token) != ContentDialogResult.Primary)
        {
            return null;
        }

        var challenge = await client.GetSafeCenterCaptchaAsync(token);
        string? captchaKey = null;
        while (await ShowCaptchaAsync(challenge, token) is { } proof)
        {
            try
            {
                captchaKey = await client.SendVerificationCodeAsync(verification, challenge, proof, token);
                break;
            }
            catch (SignInException ex) when (ex.Captcha is { } nextChallenge)
            {
                challenge = nextChallenge;
            }
        }

        if (captchaKey is null)
        {
            return null;
        }

        var dialog = CreatePhoneVerificationDialog("确认");
        var input = new TextBox { Header = "短信验证码", MaxLength = 6, InputScope = new Microsoft.UI.Xaml.Input.InputScope() };
        input.InputScope.Names.Add(new Microsoft.UI.Xaml.Input.InputScopeName { NameValue = Microsoft.UI.Xaml.Input.InputScopeNameValue.Number });
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(input);
        body.Children.Add(feedback);
        dialog.Content = body;
        dialog.IsPrimaryButtonEnabled = false;
        input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = input.Text.Length == 6 && input.Text.All(char.IsAsciiDigit);
        var completion = new TaskCompletionSource<(string? Code, Exception? Error)>(TaskCreationOptions.RunContinuationsAsynchronously);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                completion.TrySetResult((await client.VerifyPhoneAsync(verification, captchaKey, input.Text, token), null));
            }
            catch (SignInException ex) when (!token.IsCancellationRequested)
            {
                SignInDiagnostics.LogFailure(this.Get<ILogger<StartupPage>>(), "密码登录安全验证", ex);
                feedback.Text = ex.Message;
                feedback.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
            catch (Exception ex)
            {
                completion.TrySetResult((null, ex));
            }
            finally
            {
                deferral.Complete();
            }
        };

        if (await ShowPhoneVerificationDialogAsync(dialog, token) != ContentDialogResult.Primary)
        {
            return null;
        }

        var (authorizationCode, failure) = await completion.Task.WaitAsync(token);
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failure);
        }

        return authorizationCode is null ? null : await client.ExchangePhoneVerificationAsync(authorizationCode, token);
    }

    private ContentDialog CreatePhoneVerificationDialog(string primaryButton)
        => new()
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Title = "验证绑定手机号",
            PrimaryButtonText = primaryButton,
            CloseButtonText = "取消",
        };

    private async Task<ContentDialogResult> ShowPhoneVerificationDialogAsync(ContentDialog dialog, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var cancellation = token.Register(() => DispatcherQueue.TryEnqueue(dialog.Hide));
        var result = await dialog.ShowAsync();
        token.ThrowIfCancellationRequested();
        return result;
    }
}
