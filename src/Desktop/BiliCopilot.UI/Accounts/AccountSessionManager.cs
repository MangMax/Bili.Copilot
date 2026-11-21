// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Resolvers.WinUICookies;
using Richasy.BiliKernel.Resolvers.WinUIToken;
using Richasy.WinUIKernel.Share.Base;
using System.Globalization;
using static BiliCopilot.UI.Toolkits.NativeSignInClient;

namespace BiliCopilot.UI.Accounts;

internal sealed record AccountSummary(long Uid, string? Name, string? AvatarUrl);

internal sealed class AccountSessionManager
{
    private readonly AccountStore _store = new();
    private readonly SemaphoreSlim _change = new(1);
    private readonly HashSet<AccountSession> _retired = [];
    private readonly Dictionary<long, (Guid LoginId, AccountCredentials Credentials)> _credentials = [];
    private AccountSession? _current;

    public AccountSession? Current
    {
        get => Volatile.Read(ref _current);
        private set => Volatile.Write(ref _current, value);
    }

    public long? CurrentUid => Current?.Credentials.GetToken()?.UserId;

    public IEnumerable<AccountSummary> SavedAccounts
        => _store.Snapshot.Accounts.Select(item => new AccountSummary(item.Uid, item.Name, item.AvatarUrl));

    public void OpenSignIn(long? expectedUid = null)
    {
        var app = GlobalDependencies.Kernel.GetRequiredService<AppViewModel>();
        if (app.Windows.OfType<StartupWindow>().FirstOrDefault(window => window.ExpectedUid == expectedUid) is { } open)
        {
            open.Activate();
            return;
        }

        new StartupWindow(expectedUid).Activate();
    }

    public async Task InitializeAsync()
    {
        if (!await _store.LoadAsync())
        {
            var legacyTokens = new WinUIBiliTokenResolver();
            var legacyCookies = new WinUIBiliCookiesResolver();
            if (legacyTokens.GetToken() is { UserId: > 0 } token)
            {
                var cookies = new Dictionary<string, string>(
                    legacyCookies.GetCookies() ?? new Dictionary<string, string>(), StringComparer.Ordinal);
                if (!AccountCredentials.CookiesMatch(token.UserId, cookies))
                {
                    cookies = new(StringComparer.Ordinal);
                }

                var account = new SavedAccount(token.UserId, Guid.NewGuid(), null, token, cookies);
                await _store.SaveAsync(account, activate: true);
                if (!await _store.LoadAsync() || Find(account.Uid)?.LoginId != account.LoginId)
                {
                    throw new InvalidDataException("无法确认已迁移的账户数据。");
                }

                legacyTokens.RemoveToken();
                legacyCookies.RemoveCookies();
            }
        }

        if (Find(_store.Snapshot.ActiveUid) is { } active)
        {
            Current = new AccountSession(active);
            TrackCredentials(Current.Credentials, active);
        }
    }

    public Task SignInAsync(AuthorizeExecutionSettings settings, long? expectedUid, CancellationToken token, bool activateOnSuccess = false)
        => SignInCoreAsync(settings, null, expectedUid, token, activateOnSuccess);

    public Task SignInAsync(AppSignInResult appLogin, long? expectedUid, CancellationToken token, bool activateOnSuccess = false)
        => SignInCoreAsync(null, appLogin, expectedUid, token, activateOnSuccess);

    private async Task SignInCoreAsync(AuthorizeExecutionSettings? settings, AppSignInResult? appLogin,
        long? expectedUid, CancellationToken token, bool activateOnSuccess)
    {
        AccountSession? candidate = null;
        var stage = "创建登录会话";
        try
        {
            candidate = new AccountSession(appLogin: appLogin);
            if (settings is not null)
            {
                stage = settings.Cookies is null ? "扫码授权" : "网页登录凭据交换";
                await candidate.Get<IAuthenticationService>().SignInAsync(settings, token);
            }

            stage = "验证账户资料";
            var (account, profile) = await PrepareAsync(candidate, expectedUid, Guid.NewGuid(), token);
            stage = "保存账户";
            await _change.WaitAsync(token);
            try
            {
                if (Current is null || CurrentUid == account.Uid || activateOnSuccess)
                {
                    await ActivateAsync(candidate, account, profile, token);
                }
                else
                {
                    await _store.SaveAsync(account, activate: false, token);
                    _credentials.Remove(account.Uid);
                    if (GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().Windows.OfType<MainWindow>().FirstOrDefault() is { } mainWindow)
                    {
                        _ = mainWindow.ShowTipAsync(ResourceToolkit.GetLocalizedString(StringNames.BiliAccountAdded), InfoType.Success);
                    }
                }
            }
            finally
            {
                _change.Release();
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            SignInDiagnostics.LogFailure(GlobalDependencies.Kernel.GetRequiredService<ILogger<AccountSessionManager>>(), stage, ex);
            throw;
        }
        finally
        {
            if (candidate is not null && !ReferenceEquals(Current, candidate))
            {
                await candidate.DisposeAsync();
            }
        }
    }

    public async Task SwitchAsync(long uid)
    {
        await _change.WaitAsync();
        try
        {
            if (CurrentUid == uid || Find(uid) is not { } account)
            {
                return;
            }

            var candidate = _credentials.TryGetValue(uid, out var login) && login.LoginId == account.LoginId
                ? new AccountSession(login.Credentials)
                : new AccountSession(account);
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var (updated, profile) = await PrepareAsync(candidate, uid, account.LoginId, timeout.Token);
                await ActivateAsync(candidate, updated, profile, timeout.Token);
            }
            finally
            {
                if (!ReferenceEquals(Current, candidate))
                {
                    await candidate.DisposeAsync();
                }
            }
        }
        finally
        {
            _change.Release();
        }
    }

    public async Task SaveProfileAsync(AccountSession session, UserDetailProfile profile)
    {
        if (!ReferenceEquals(Current, session) || Find(CurrentUid) is not { } saved)
        {
            return;
        }

        var name = profile.User.Name;
        var avatar = profile.User.Avatar?.Uri?.ToString();
        if (name == saved.Name && avatar == saved.AvatarUrl)
        {
            return;
        }

        await _change.WaitAsync();
        try
        {
            if (ReferenceEquals(Current, session) && Find(saved.Uid) is { } latest)
            {
                await _store.SaveProfileAsync(latest.Uid, latest.LoginId, name, avatar);
            }
        }
        finally
        {
            _change.Release();
        }
    }

    public async Task SignOutAsync(AccountSession session)
    {
        await _change.WaitAsync();
        try
        {
            if (!ReferenceEquals(Current, session) || CurrentUid is not { } uid)
            {
                return;
            }

            await session.Get<NotificationViewModel>().ClearOnSignOutAsync();
            await _store.RemoveAsync(uid);
            _credentials.Remove(uid);
            var app = session.Get<AppViewModel>();
            foreach (var player in app.Players.Where(player => ReferenceEquals(AccountSession.Find(player), session)).ToArray())
            {
                await player.CloseForAccountSwitchAsync();
            }

            Current = null;
            app.RestartCommand.Execute(default);
        }
        finally
        {
            _change.Release();
        }
    }

    public async Task CollectRetiredAsync(AccountSession? session)
    {
        if (session is not null && _retired.Contains(session)
            && !session.Get<AppViewModel>().Players.Any(player => ReferenceEquals(AccountSession.Find(player), session)))
        {
            _retired.Remove(session);
            await session.DisposeAsync();
        }
    }

    private SavedAccount? Find(long? uid) => _store.Snapshot.Accounts.FirstOrDefault(item => item.Uid == uid);

    private static async Task<(SavedAccount Account, UserDetailProfile Profile)> PrepareAsync(
        AccountSession session, long? expectedUid, Guid loginId, CancellationToken token)
    {
        await session.Get<IAuthenticationService>().EnsureTokenAsync(token);
        var profile = await session.Get<IMyProfileService>().GetMyProfileAsync(token);
        var (credentials, cookies) = session.Credentials.Snapshot();
        if (credentials is not { UserId: > 0 } || (expectedUid.HasValue && credentials.UserId != expectedUid)
            || !AccountCredentials.CookiesMatch(credentials.UserId, cookies)
            || !long.TryParse(profile.User.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var uid) || uid != credentials.UserId)
        {
            throw new InvalidOperationException("登录账户与所选账户不匹配。");
        }

        return (new SavedAccount(uid, loginId, profile.User.Name, credentials, cookies, profile.User.Avatar?.Uri?.ToString(),
            session.Credentials.AppDevice), profile);
    }

    private async Task ActivateAsync(AccountSession candidate, SavedAccount account, UserDetailProfile profile, CancellationToken token)
    {
        var old = Current;
        var app = candidate.Get<AppViewModel>();
        var mainWindow = app.Windows.OfType<MainWindow>().FirstOrDefault();
        var layout = mainWindow is null ? null : candidate.Create(() => new RootLayout());
        candidate.Get<AccountViewModel>().ApplyPreparedProfile(profile);
        await _store.SaveAsync(account, activate: true, token, candidate.Credentials);
        TrackCredentials(candidate.Credentials, account);
        Current = candidate;
        try
        {
            if (mainWindow is not null && layout is not null)
            {
                mainWindow.ApplyAccountLayout(layout);
            }
            else
            {
                app.ShowMainWindowAfterSignIn();
            }
        }
        catch
        {
            Current = old;
            await _store.SetActiveAsync(old?.Credentials.GetToken()?.UserId);
            throw;
        }

        var previousNotifications = old?.Get<NotificationViewModel>();
        previousNotifications?.Stop();
        app.IsInitialLoading = false;
        candidate.Get<NotificationViewModel>().Start();
        if (old is null)
        {
            return;
        }


        foreach (var player in app.Players.Where(player => player.UseIntegrationOperation))
        {
            var uid = AccountSession.Find(player)?.Credentials.GetToken()?.UserId;
            player.PlaybackAccount = uid is null || uid == account.Uid ? null : Find(uid)?.Name ?? $"UID {uid}";
        }

        var independentPlayers = app.Players.Where(player => !player.UseIntegrationOperation
            && ReferenceEquals(AccountSession.Find(player), old)).ToArray();
        foreach (var player in independentPlayers)
        {
            try
            {
                await player.CloseForAccountSwitchAsync();
            }
            catch (Exception ex)
            {
                candidate.Get<ILogger<AccountSessionManager>>().LogWarning(ex, "关闭独立播放器失败。");
            }
        }

        if (previousNotifications is not null)
        {
            try
            {
                await previousNotifications.StopAndWaitAsync();
            }
            catch (Exception ex)
            {
                candidate.Get<ILogger<AccountSessionManager>>().LogWarning(ex, "等待旧账户通知任务结束失败。");
            }
        }

        _retired.Add(old);
        await CollectRetiredAsync(old);
    }

    private void TrackCredentials(AccountCredentials credentials, SavedAccount account)
    {
        if (_credentials.TryGetValue(account.Uid, out var login) && login.LoginId == account.LoginId)
        {
            return;
        }
        var uid = account.Uid;
        var loginId = account.LoginId;
        _credentials[uid] = (loginId, credentials);
        credentials.Changed += () => _ = PersistCredentialsAsync(credentials, uid, loginId);
    }

    private async Task PersistCredentialsAsync(AccountCredentials credentials, long uid, Guid loginId)
    {
        try
        {
            await _store.SaveCredentialsAsync(uid, loginId, credentials);
        }
        catch (Exception ex)
        {
            GlobalDependencies.Kernel.GetRequiredService<ILogger<AccountSessionManager>>().LogWarning(ex, "保存更新后的账户凭据失败。");
        }
    }
}
