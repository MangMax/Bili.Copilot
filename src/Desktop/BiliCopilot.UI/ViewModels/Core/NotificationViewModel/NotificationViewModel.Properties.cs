// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.Extensions.Logging;
using BiliCopilot.UI.Services.MomentNotifications;
using Richasy.BiliKernel.Bili.Authorization;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 通知视图模型.
/// </summary>
public sealed partial class NotificationViewModel
{
    private readonly ILogger<NotificationViewModel> _logger;
    private readonly MomentNotificationSource _source;
    private readonly IBiliTokenResolver _tokenResolver;
    private readonly IAuthenticationService _authenticationService;
    private readonly HashSet<long> _accountsToReset = [];

    private DispatcherTimer? _timer;
    private CancellationTokenSource? _scanCancellation;
    private bool _isTileSupport;
    private Task? _checkTask;
    private bool _checkRequested;
    private bool _scopeChanged;
}
