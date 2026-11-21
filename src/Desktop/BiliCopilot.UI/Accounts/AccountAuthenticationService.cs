// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Authorizers.TV;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;

namespace BiliCopilot.UI.Accounts;

internal sealed class AccountAuthenticationService(AccountCredentials credentials, TVAuthenticationService tvAuthentication)
    : IAuthenticationService
{
    public Task SignInAsync(AuthorizeExecutionSettings? settings = null, CancellationToken cancellationToken = default)
        => tvAuthentication.SignInAsync(settings, cancellationToken);

    public Task SignOutAsync(CancellationToken cancellationToken = default)
        => tvAuthentication.SignOutAsync(cancellationToken);

    public async Task EnsureTokenAsync(CancellationToken cancellationToken = default)
    {
        await credentials.RefreshLock.WaitAsync(cancellationToken);
        try
        {
            if (credentials.AppDevice is not { } device)
            {
                await tvAuthentication.EnsureTokenAsync(cancellationToken);
                return;
            }

            var (token, cookies) = credentials.Snapshot();
            if (token is not { UserId: > 0, AccessToken.Length: > 0, RefreshToken.Length: > 0 })
            {
                throw new InvalidOperationException("登录已失效，请重新登录。");
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() < token.ExpiresIn)
            {
                return;
            }

            using var client = new NativeSignInClient(device);
            try
            {
                var refreshed = await client.RefreshAsync(token, cookies, cancellationToken);
                credentials.ReplaceAppCredentials(refreshed);
            }
            catch (NativeSignInClient.SignInException ex) when (ex.BusinessCode is -101 or -400 or -105 or 86095)
            {
                throw new InvalidOperationException("B 站拒绝续期，请重新登录。");
            }
        }
        finally
        {
            credentials.RefreshLock.Release();
        }
    }
}
