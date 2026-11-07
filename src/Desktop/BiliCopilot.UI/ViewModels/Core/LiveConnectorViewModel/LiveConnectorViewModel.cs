// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.MpvKernel.Player;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.System;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播连接器：拉取直播间详情，维护房间标题/主播/状态，并给播放器提供面板入口。
/// </summary>
/// <remarks>
/// 直播没有上一集/下一集，也没有清晰度选择（#194），因此这里只做"进入房间"这一件事。
/// 关闭窗口时 <see cref="CancelPending"/> 会取消仍在进行的详情请求。
/// </remarks>
public sealed partial class LiveConnectorViewModel : ViewModelBase, IPlayerConnectorViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveConnectorViewModel"/> class.
    /// </summary>
    /// <param name="parent">播放器视图模型.</param>
    public LiveConnectorViewModel(PlayerViewModel parent)
    {
        Parent = parent;
        _logger = this.Get<ILogger<LiveConnectorViewModel>>();
        QualityText = ResourceToolkit.GetLocalizedString(StringNames.LiveQualityPlaceholder);
        LineText = ResourceToolkit.GetLocalizedString(StringNames.LiveLinePlaceholder);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(MediaSnapshot snapshot, IMpvMediaSourceResolver sourceResolver)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Live is null)
        {
            throw new InvalidOperationException("直播媒体快照缺少直播间信息.");
        }

        _snapshot = snapshot;
        // 换房/重进：先停掉上一房间的弹幕连接与注入，再重置房间状态。
        await StopDanmakuAsync();
        Session.Reset(snapshot.Live);
        ErrorText = default;
        IsLoading = true;
        SyncFromSession();
        var request = Session.BeginRequest();
        try
        {
            _view = await this.Get<IPlayerService>().GetLivePageDetailAsync(
                snapshot.Live.Identifier,
                request.CancellationToken);
            if (!Session.CompletePage(request.Generation, _view))
            {
                return;
            }

            SyncFromSession();
            UpdateProperties();
            // 弹幕连接不阻塞播放初始化：认证与重连在后台进行，失败只影响弹幕状态。
            StartDanmakuInBackground(snapshot.Live.Identifier.Id);
        }
        catch (OperationCanceledException)
        {
            // 关闭窗口或换房：迟到的详情不再写回。
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取直播间 {RoomId} 信息失败", snapshot.Live?.Identifier.Id);
            if (Session.Fail(request.Generation, LiveFailureClassifier.Classify(ex), ex.Message))
            {
                SyncFromSession();
                ErrorText = ResourceToolkit.GetLocalizedString(LiveFailureText.GetMessage(Session.FailureKind));
            }
        }
        finally
        {
            IsLoading = false;
            SyncFromSession();
            PlaylistInitialized?.Invoke(this, new PlaylistInitializedEventArgs(false, false, default, default));
        }
    }

    /// <inheritdoc/>
    public Task PlayNextAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task PlayPreviousAsync() => Task.CompletedTask;

    /// <summary>
    /// 取消仍在进行的房间详情请求并停止弹幕连接；关闭播放器时调用。
    /// </summary>
    public void CancelPending()
    {
        Session.CancelPending();
        StopDanmakuInBackground();
    }

    /// <summary>
    /// 打开播放器侧栏（#197 聊天侧栏的容器）。
    /// </summary>
    [RelayCommand]
    private void OpenPanel() => RequestOpenExtraPanel?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 在系统浏览器打开当前直播间，作为地址/鉴权/解码失败时的兜底。
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private Task OpenInBrowserAsync()
        => Launcher.LaunchUriAsync(new Uri(GetWebUrl())).AsTask();

    private void SyncFromSession()
    {
        Title = Session.Title;
        UpName = Session.AnchorName;
        Cover = Session.Page?.Information.Identifier.Cover?.Uri;
        IsLiving = Session.IsLiving;
        TagName = Session.TagName;
        RoomStatusText = ResourceToolkit.GetLocalizedString(LiveFailureText.GetRoomStatus(IsLiving, IsLoading && Session.Page is null));
    }

    private void UpdateProperties()
    {
        var subtitle = string.IsNullOrEmpty(UpName) ? RoomStatusText : UpName;
        PropertiesUpdated?.Invoke(this, new PlayerInformationUpdatedEventArgs(Title, subtitle, Cover));
    }

    private string GetWebUrl()
        => $"https://live.bilibili.com/{_snapshot?.Live?.Identifier.Id ?? Session.RoomId}";
}
