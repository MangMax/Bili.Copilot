// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播连接器视图模型.
/// </summary>
/// <remarks>
/// 扩展接缝（供后续子票使用，本票不实现）：
/// #194 画质/线路/断流恢复——在 <see cref="Session"/> 上扩展候选与刷新状态，
///       播放地址刷新统一走 PlayerViewModel 的 PlaybackRefreshController。
/// #196 弹幕准入/上屏——在会话就绪后调用 DanmakuRenderViewModel.AddDanmaku，不在这里直连内核。
/// #197 聊天侧栏——由 LivePlayerPanel 承载列表，Connector 只暴露与控制连接状态。
/// #198 房间状态/实时事件——把标题、在线人数、开播/下播、PLAYURL_RELOAD 写回 Session 与 PropertiesUpdated。
/// </remarks>
public sealed partial class LiveConnectorViewModel
{
    private readonly Microsoft.Extensions.Logging.ILogger<LiveConnectorViewModel> _logger;
    private MediaSnapshot _snapshot;
    internal LivePlayerView? _view;

    /// <inheritdoc/>
    public event EventHandler<PlaylistInitializedEventArgs> PlaylistInitialized;

    /// <inheritdoc/>
    public event EventHandler<MediaSnapshot> NewMediaRequest;

    /// <inheritdoc/>
    public event EventHandler RequestOpenExtraPanel;

    /// <inheritdoc/>
    public event EventHandler<PlayerInformationUpdatedEventArgs> PropertiesUpdated;

    /// <summary>
    /// Gets 本次观看的直播会话状态.
    /// </summary>
    public LiveSession Session { get; } = new();

    /// <summary>
    /// 请求播放另一个直播间（#198 房间切换的接缝；本票不触发）.
    /// </summary>
    /// <param name="snapshot">新的直播快照.</param>
    internal void RequestNewMedia(MediaSnapshot snapshot) => NewMediaRequest?.Invoke(this, snapshot);

    /// <summary>
    /// Gets 播放视图模型.
    /// </summary>
    public PlayerViewModel Parent { get; }

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? UpName { get; set; }

    [ObservableProperty]
    public partial Uri? Cover { get; set; }

    [ObservableProperty]
    public partial bool IsLiving { get; set; }

    [ObservableProperty]
    public partial string? TagName { get; set; }

    /// <summary>
    /// 房间状态文案（正在直播 / 未开播 / 连接中）.
    /// </summary>
    [ObservableProperty]
    public partial string? RoomStatusText { get; set; }

    /// <summary>
    /// 画质占位文案（#194 接线）.
    /// </summary>
    [ObservableProperty]
    public partial string? QualityText { get; set; }

    /// <summary>
    /// 线路占位文案（#194 接线）.
    /// </summary>
    [ObservableProperty]
    public partial string? LineText { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// 可读的失败原因；为空表示没有失败.
    /// </summary>
    [ObservableProperty]
    public partial string? ErrorText { get; set; }
}
