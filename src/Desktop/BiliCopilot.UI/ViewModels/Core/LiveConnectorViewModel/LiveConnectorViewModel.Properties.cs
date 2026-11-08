// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.ObjectModel;
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
    /// Gets or sets 直播会话使用的时间来源；测试注入假时钟，生产默认系统时钟.
    /// </summary>
    /// <remarks>
    /// 系统消息、醒目留言倒计时与"重新取址"节流都必须读这里，不能各自调用
    /// <c>DateTimeOffset.UtcNow</c>，否则假时钟无法覆盖这些路径。
    /// </remarks>
    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

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
    /// 当前画质文案.
    /// </summary>
    [ObservableProperty]
    public partial string? QualityText { get; set; }

    /// <summary>
    /// 当前线路文案.
    /// </summary>
    [ObservableProperty]
    public partial string? LineText { get; set; }

    /// <summary>
    /// 内核返回的可选画质.
    /// </summary>
    public ObservableCollection<LiveQualityOption> Qualities { get; } = [];

    /// <summary>
    /// 排序后的线路候选（含同一候选内的多条地址）.
    /// </summary>
    public ObservableCollection<LiveLineOption> Lines { get; } = [];

    /// <summary>
    /// 当前选中的画质；修改会重新取址并原位重开.
    /// </summary>
    [ObservableProperty]
    public partial LiveQualityOption? SelectedQuality { get; set; }

    /// <summary>
    /// 当前选中的线路；只影响首选地址，不改播放器实例.
    /// </summary>
    [ObservableProperty]
    public partial LiveLineOption? SelectedLine { get; set; }

    /// <summary>
    /// 弹幕连接状态文案（连接中 / 已连接 / 重连中 / 已断开）.
    /// </summary>
    [ObservableProperty]
    public partial string? DanmakuConnectionText { get; set; }

    /// <summary>
    /// 弹幕准入计数（收到 / 上屏 / 合并 / 屏蔽 / 限速丢 / 队满丢 / 超时丢）.
    /// </summary>
    [ObservableProperty]
    public partial string? DanmakuCountersText { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// 可读的失败原因；为空表示没有失败.
    /// </summary>
    [ObservableProperty]
    public partial string? ErrorText { get; set; }
}