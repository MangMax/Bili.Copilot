// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播间的终态呈现（#198）：主播已下播 / 直播被切断。
/// </summary>
/// <remarks>
/// 房间事件是权威状态：终态下不再自动重连或自动刷新，也不显示"播放已结束"。
/// 界面给出"返回"与"在浏览器打开"两个出口；开播事件由连接器调用
/// <see cref="ClearLiveRoomTerminal"/> 恢复。该状态不写历史、不改进度。
/// </remarks>
public sealed partial class PlayerViewModel
{
    /// <summary>
    /// Gets 直播是否处于终态（已下播 / 被切断）.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLiveRoomTerminal { get; set; }

    /// <summary>
    /// Gets 终态标题.
    /// </summary>
    [ObservableProperty]
    public partial string? LiveRoomTerminalTitle { get; set; }

    /// <summary>
    /// Gets 终态说明.
    /// </summary>
    [ObservableProperty]
    public partial string? LiveRoomTerminalDescription { get; set; }

    /// <summary>
    /// 进入终态：停掉错误提示、隐藏黑底遮挡，并收起控制层让状态卡片可点。
    /// </summary>
    /// <param name="phase">终态类型.</param>
    /// <param name="description">可读说明.</param>
    internal void ApplyLiveRoomTerminal(LiveRoomPhase phase, string? description)
    {
        LiveRoomTerminalTitle = ResourceToolkit.GetLocalizedString(
            phase == LiveRoomPhase.CutOff ? StringNames.LiveRoomCutOffTitle : StringNames.LiveRoomNotStartedTitle);
        LiveRoomTerminalDescription = description;
        IsLiveRoomTerminal = true;
        IsConnecting = false;
        ErrorMessage = default;
        ErrorActionText = default;
        HasErrorAction = false;
        IsEnd = false;
        CheckBlackBackgroundVisible();
        // 终态下不再保留播放器重连循环：暂停当前流，避免无限缓冲。
        _ = PauseLivePlaybackAsync();
    }

    private async Task PauseLivePlaybackAsync()
    {
        try
        {
            if (Client?.IsDisposed == false)
            {
                await Client.PauseAsync();
            }
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(ex, "暂停直播画面失败");
            }
        }
    }

    /// <summary>
    /// 离开终态（主播重新开播）。
    /// </summary>
    internal void ClearLiveRoomTerminal()
    {
        IsLiveRoomTerminal = false;
        LiveRoomTerminalTitle = default;
        LiveRoomTerminalDescription = default;
        ErrorMessage = default;
        CheckBlackBackgroundVisible();
    }

    /// <summary>
    /// 房间事件触发的播放地址重载：与手动刷新共用同一次单飞准入，永不新建播放器。
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    internal Task RequestLivePlayUrlReloadAsync()
    {
        if (!IsLive || IsLiveRoomTerminal || !IsSessionCurrent())
        {
            return Task.CompletedTask;
        }

        return RefreshPlaybackAsync();
    }

    /// <summary>
    /// 终态下返回上一级：关闭播放窗口。
    /// </summary>
    [RelayCommand]
    private void BackFromLiveRoom()
    {
        if (IsExtraPanelVisible)
        {
            IsExtraPanelVisible = false;
        }

        BackCommand.Execute(default);
    }

    /// <summary>
    /// 终态下在浏览器打开当前直播间。
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private Task OpenLiveRoomInBrowserAsync() => OpenLiveInBrowserAsync();

    /// <summary>
    /// 打开直播侧栏（房间状态与聊天）：窄窗口与竖屏下 Widget 被收起时仍可进入。
    /// </summary>
    [RelayCommand]
    private void OpenLivePanel()
    {
        if (!IsLive || Connector is not LiveConnectorViewModel connector)
        {
            return;
        }

        connector.OpenPanelCommand.Execute(default);
    }
}
