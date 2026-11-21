// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.DependencyInjection;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Models.Authorization;
using System.Globalization;
using System.Runtime.CompilerServices;
using static BiliCopilot.UI.Toolkits.NativeSignInClient;

namespace BiliCopilot.UI.Accounts;

internal sealed class AccountCredentials(SavedAccount? account = null, AppSignInResult? appLogin = null) : IBiliTokenResolver, IBiliCookiesResolver
{
    private readonly object _gate = new();
    private BiliToken? _token = appLogin?.Token ?? account?.Token;
    private Dictionary<string, string> _cookies = (appLogin?.Cookies ?? account?.Cookies) is { } cookies
        ? new(cookies, StringComparer.Ordinal) : new(StringComparer.Ordinal);

    public AppLoginDevice? AppDevice { get; } = appLogin?.Device ?? account?.AppDevice;

    public bool IsAndroidHdToken => AppDevice is not null;

    internal SemaphoreSlim RefreshLock { get; } = new(1);

    public event Action? Changed;

    public BiliToken? GetToken()
    {
        lock (_gate)
        {
            return _token;
        }
    }

    public IDictionary<string, string> GetCookies()
    {
        lock (_gate)
        {
            return new Dictionary<string, string>(_cookies, StringComparer.Ordinal);
        }
    }

    public string GetCookieString() => string.Join("; ", GetCookies().Select(item => $"{item.Key}={item.Value}"));

    public void SaveToken(BiliToken token)
    {
        lock (_gate)
        {
            if ((_token is { UserId: > 0 } current && current.UserId != token.UserId) || !CookiesMatch(token.UserId, _cookies))
            {
                throw new InvalidOperationException("凭据与当前账户不匹配。");
            }

            _token = token;
        }

        Changed?.Invoke();
    }

    public void SaveCookies(IDictionary<string, string> cookies)
    {
        lock (_gate)
        {
            if (_token is { UserId: > 0 } token && !CookiesMatch(token.UserId, cookies))
            {
                throw new InvalidOperationException("Cookie 与当前账户不匹配。");
            }

            _cookies = new(cookies, StringComparer.Ordinal);
        }

        Changed?.Invoke();
    }

    public void ReplaceAppCredentials(AppSignInResult result)
    {
        lock (_gate)
        {
            if (result.Device != AppDevice || _token?.UserId != result.Token.UserId || !CookiesMatch(result.Token.UserId, result.Cookies))
            {
                throw new InvalidOperationException("更新后的凭据与当前账户不匹配。");
            }

            _token = result.Token;
            _cookies = new(result.Cookies, StringComparer.Ordinal);
        }

        Changed?.Invoke();
    }

    public static bool CookiesMatch(long uid, IDictionary<string, string> cookies)
        => !cookies.TryGetValue("DedeUserID", out var account)
            || (long.TryParse(account, NumberStyles.None, CultureInfo.InvariantCulture, out var cookieUid) && cookieUid == uid);

    public void RemoveToken()
    {
        lock (_gate)
        {
            _token = null;
        }
    }

    public void RemoveCookies() => SaveCookies(new Dictionary<string, string>());

    public (BiliToken? Token, Dictionary<string, string> Cookies) Snapshot()
    {
        lock (_gate)
        {
            return (_token, new(_cookies, StringComparer.Ordinal));
        }
    }
}

internal sealed class AccountSession : IAsyncDisposable
{
    private static readonly ConditionalWeakTable<object, AccountSession> Owners = new();
    private static readonly AsyncLocal<AccountSession?> Construction = new();
    private readonly ServiceProvider _services;

    public AccountSession(SavedAccount? account = null, AppSignInResult? appLogin = null)
        : this(new AccountCredentials(account, appLogin))
    {
    }

    public AccountSession(AccountCredentials credentials)
    {
        Credentials = credentials;
        _services = GlobalDependencies.CreateAccountProvider(credentials);
    }

    public AccountCredentials Credentials { get; }

    // An explicit owner always wins. The ambient value is only used while constructing the UI or a service.
    public static AccountSession? Find(object owner)
        => Owners.TryGetValue(owner, out var session) ? session : Construction.Value;

    public void Bind(object owner)
    {
        if (owner is not (App or AppViewModel or MainWindow or AppUpdateViewModel or SettingsPageViewModel))
        {
            Owners.TryAdd(owner, this);
        }
    }

    public T Get<T>() where T : class
        => Create(() =>
        {
            var service = _services.GetService<T>() ?? GlobalDependencies.Kernel.GetRequiredService<T>();
            if (service is Richasy.WinUIKernel.Share.ViewModels.ViewModelBase)
            {
                Bind(service);
            }

            return service;
        });

    public T Create<T>(Func<T> create)
    {
        var previous = Construction.Value;
        Construction.Value = this;
        try
        {
            return create();
        }
        finally
        {
            Construction.Value = previous;
        }
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
