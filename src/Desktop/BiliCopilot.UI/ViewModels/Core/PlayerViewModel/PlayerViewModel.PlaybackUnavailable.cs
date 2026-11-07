// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Bili.Media;
using Windows.System;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 播放不可用时的提示与操作.
/// </summary>
public sealed partial class PlayerViewModel
{
    private const string VipPageUrl = "https://account.bilibili.com/big";

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
            default:
                break;
        }
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
