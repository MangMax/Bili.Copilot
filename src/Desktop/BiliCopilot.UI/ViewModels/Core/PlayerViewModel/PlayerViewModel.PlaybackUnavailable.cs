// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Bili.Media;
using Richasy.MpvKernel.Core.Enums;
using Windows.System;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 播放不可用时的提示与操作.
/// </summary>
public sealed partial class PlayerViewModel
{
    private const string VipPageUrl = "https://account.bilibili.com/big";
    private CancellationTokenSource? _failureTipCts;

    private PlaybackUnavailableAction _playbackUnavailableAction;

    /// <summary>
    /// 不可用提示旁的操作文案.
    /// </summary>
    [ObservableProperty]
    public partial string? ErrorActionText { get; set; }

    /// <summary>
    /// 是否有可执行的操作.
    /// </summary>
    [ObservableProperty]
    public partial bool HasErrorAction { get; set; }

    /// <summary>
    /// 当前是否只能试看.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPreviewPlayback { get; set; }

    /// <summary>
    /// 展示播放不可用的原因与可执行操作.
    /// </summary>
    /// <param name="reason">内核给出的原因；为空表示普通失败.</param>
    internal void ShowPlaybackUnavailable(PgcPlaybackUnavailableReason? reason)
    {
        var isVip = this.Get<AccountViewModel>().MyProfile?.IsVip == true;
        var notice = PlaybackUnavailableClassifier.Classify(reason, isVip);
        _playbackUnavailableAction = notice.Action;
        ErrorMessage = ResourceToolkit.GetLocalizedString(ToMessageName(notice.Message));
        ErrorActionText = notice.Action switch
        {
            PlaybackUnavailableAction.ReSignIn => ResourceToolkit.GetLocalizedString(StringNames.ReSignIn),
            PlaybackUnavailableAction.OpenVipPage => ResourceToolkit.GetLocalizedString(StringNames.OpenVipPage),
            _ => default,
        };
        HasErrorAction = notice.Action is not PlaybackUnavailableAction.None;
    }

    private static StringNames ToMessageName(PlaybackUnavailableMessage message)
        => message switch
        {
            PlaybackUnavailableMessage.LoginRequired => StringNames.PlaybackLoginRequired,
            PlaybackUnavailableMessage.NeedVip => StringNames.PlaybackNeedVip,
            PlaybackUnavailableMessage.SessionExpired => StringNames.PlaybackSessionExpired,
            PlaybackUnavailableMessage.RegionRestricted => StringNames.PlaybackRegionRestricted,
            PlaybackUnavailableMessage.ContentUnavailable => StringNames.PlaybackContentUnavailable,
            _ => StringNames.PlaybackSourceFailed,
        };

    [RelayCommand]
    private void RunPlaybackErrorAction()
    {
        switch (_playbackUnavailableAction)
        {
            case PlaybackUnavailableAction.ReSignIn:
                OpenSignInWindow();
                break;
            case PlaybackUnavailableAction.OpenVipPage:
                _ = OpenVipPageAsync();
                break;
            case PlaybackUnavailableAction.OpenInBrowser:
                _ = OpenLiveInBrowserAsync();
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 展示直播的失败原因，并把"在浏览器打开"作为可执行操作。
    /// </summary>
    /// <param name="kind">失败分类.</param>
    /// <param name="canRetry">候选与重新取址都失败时为 true，提示从哪里手动重试.</param>
    internal void ShowLivePlaybackFailure(LiveFailureKind kind, bool canRetry = false)
    {
        if (kind == LiveFailureKind.Cancelled)
        {
            return;
        }

        // 直播取流/切线路会有短暂失败，恢复通常在一两秒内完成；
        // 先保持加载态，确认仍未恢复再显示错误，避免一闪而过的打断。
        _failureTipCts?.Cancel();
        _failureTipCts = new CancellationTokenSource();
        _ = ShowLivePlaybackFailureAsync(kind, canRetry, _failureTipCts.Token);
    }

    private async Task ShowLivePlaybackFailureAsync(LiveFailureKind kind, bool canRetry, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested
            || Player?.PlaybackState == MpvPlayerState.Playing
            || IsLiveRoomTerminal)
        {
            return;
        }

        _playbackUnavailableAction = PlaybackUnavailableAction.OpenInBrowser;
        ErrorMessage = ResourceToolkit.GetLocalizedString(canRetry ? StringNames.LivePlaybackRetry : LiveFailureText.GetMessage(kind));
        ErrorActionText = ResourceToolkit.GetLocalizedString(StringNames.OpenInBrowser);
        HasErrorAction = true;
        IsConnecting = false;
        CheckBlackBackgroundVisible();
    }

    private Task OpenLiveInBrowserAsync()
    {
        if (_snapshot?.Live?.Identifier.Id is not { Length: > 0 } roomId)
        {
            return Task.CompletedTask;
        }

        return LiveRoomUrl.Create(roomId) is { } url
            ? Launcher.LaunchUriAsync(url).AsTask()
            : Task.CompletedTask;
    }

    [RelayCommand]
    private Task OpenVipPageAsync() => Launcher.LaunchUriAsync(new Uri(VipPageUrl)).AsTask();

    private void OpenSignInWindow()
    {
        var window = this.Get<AppViewModel>().Windows.FirstOrDefault(p => p is WebSignInWindow);
        if (window is not null)
        {
            window.Activate();
            return;
        }

        new WebSignInWindow().Activate();
    }
}
