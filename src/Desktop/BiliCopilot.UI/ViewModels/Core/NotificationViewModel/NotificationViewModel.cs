// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Http;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 通知视图模型.
/// </summary>
public sealed partial class NotificationViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationViewModel"/> class.
    /// </summary>
    public NotificationViewModel(
        IMomentDiscoveryService momentService,
        IRelationshipService relationshipService,
        IAuthenticationService authenticationService,
        IBiliTokenResolver tokenResolver,
        IBiliCookiesResolver cookiesResolver,
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator,
        ILogger<NotificationViewModel> logger)
    {
        _source = new(httpClient, authenticator, cookiesResolver, relationshipService, momentService);
        _authenticationService = authenticationService;
        _tokenResolver = tokenResolver;
        _logger = logger;
    }

    internal long AccountId => _tokenResolver.GetToken()?.UserId ?? 0;

    internal void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _timer.Tick += OnTimerTick;
        _ = InitializeTilesAsync();
        ApplySettings();
    }

    internal void ApplySettings()
    {
        if (_timer is null)
        {
            return;
        }

        _scanCancellation?.Cancel();
        _timer.Stop();
        var enabled = SettingsToolkit.ReadLocalSetting(SettingNames.IsNotificationEnabled, true);
        var videoEnabled = enabled && VideoEnabled;
        var otherEnabled = enabled && OtherDynamicsEnabled;
        if (AccountId is > 0 and var account)
        {
            try
            {
                ResetDisabledState(account, videoEnabled, otherEnabled);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "重置已关闭的动态通知状态失败。");
            }

            if (!videoEnabled && !otherEnabled)
            {
                _accountsToReset.Add(account);
            }
        }

        _scopeChanged = videoEnabled || otherEnabled;
        _timer.Start();

        RequestCheck();
    }

    internal void Stop()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }

        _scanCancellation?.Cancel();
    }

    internal async Task StopAndWaitAsync()
    {
        Stop();
        if (_checkTask is { } check)
        {
            await check;
        }
    }

    internal async Task ClearOnSignOutAsync()
    {
        var account = AccountId;
        Stop();
        if (account <= 0)
        {
            return;
        }

        try
        {
            if (_checkTask is { } check)
            {
                await check;
            }

            await ClearNotificationsAsync(account);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "退出登录时清理动态通知失败。");
        }
    }

    private static bool VideoEnabled => SettingsToolkit.ReadLocalSetting(SettingNames.IsVideoMomentNotificationEnabled, true);

    private static bool OtherDynamicsEnabled => SettingsToolkit.ReadLocalSetting(SettingNames.IsOtherMomentNotificationEnabled, false);

    private async Task InitializeTilesAsync()
    {
        try
        {
            var entry = (await Package.Current.GetAppListEntriesAsync()).FirstOrDefault();
            _isTileSupport = entry is not null && StartScreenManager.GetDefault().SupportsAppListEntry(entry);
            if (_isTileSupport)
            {
                TileUpdateManager.CreateTileUpdaterForApplication().EnableNotificationQueue(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "无法初始化动态磁贴。");
        }
    }

    private void RequestCheck()
    {
        _checkRequested = true;
        if (_checkTask is not { IsCompleted: false })
        {
            _checkTask = CheckLoopAsync();
        }
    }

    private async Task CheckLoopAsync()
    {
        while (_checkRequested && _timer is not null)
        {
            _checkRequested = false;
            using var cancellation = new CancellationTokenSource();
            _scanCancellation = cancellation;
            try
            {
                foreach (var account in _accountsToReset.ToArray())
                {
                    try
                    {
                        ResetDisabledState(account, videoEnabled: false, otherDynamicsEnabled: false);
                        await ClearNotificationsAsync(account);
                        _accountsToReset.Remove(account);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "清理已关闭的 Windows 动态通知失败。");
                    }
                }

                cancellation.Token.ThrowIfCancellationRequested();
                if (_timer?.IsEnabled == true && _tokenResolver.GetToken() is { UserId: > 0, AccessToken.Length: > 0 } token)
                {
                    var enabled = SettingsToolkit.ReadLocalSetting(SettingNames.IsNotificationEnabled, true);
                    var videoEnabled = enabled && VideoEnabled;
                    var otherEnabled = enabled && OtherDynamicsEnabled;
                    await _authenticationService.EnsureTokenAsync(cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (AccountId == token.UserId)
                    {
                        var inAppCheck = this.Get<MomentPageViewModel>().CheckForUpdatesAsync(token.UserId, cancellation.Token);
                        try
                        {
                            if (_scopeChanged)
                            {
                                await ApplyNotificationScopeAsync(token.UserId, videoEnabled, otherEnabled, cancellation.Token);
                                cancellation.Token.ThrowIfCancellationRequested();
                                _scopeChanged = false;
                            }

                            if (videoEnabled || otherEnabled)
                            {
                                await CheckMomentsAsync(token.UserId, videoEnabled, otherEnabled, cancellation.Token);
                            }
                        }
                        finally
                        {
                            await inAppCheck;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检查关注动态通知失败。");
            }
            finally
            {
                _scanCancellation = null;
            }
        }
    }

    private void OnTimerTick(object? sender, object e)
        => RequestCheck();
}
