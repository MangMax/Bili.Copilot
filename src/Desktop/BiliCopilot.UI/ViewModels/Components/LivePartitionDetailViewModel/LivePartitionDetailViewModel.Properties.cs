// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 直播分区详情视图模型.
/// </summary>
public sealed partial class LivePartitionDetailViewModel
{
    private readonly ILogger<LivePartitionDetailViewModel> _logger;
    private readonly ILiveDiscoveryService _service;
    private readonly Dictionary<LiveTag, List<LiveItemViewModel>> _childPartitionRoomCache = new();
    private readonly Dictionary<LiveTag, int> _childPartitionOffsetCache = new();

    private bool _preventLoadMore;

    // 切换子分区时旧请求可能仍在飞行，它结束后必须为最后选择补发一次请求.
    private LiveTag? _pendingRoomTag;

    private bool _roomLoadInProgress;

    [ObservableProperty]
    public partial List<LiveTag>? Children { get; set; }

    /// <summary>
    /// 可供选择的子分区；没有可用标签（首次加载失败或该分区没有标签）时为 null.
    /// </summary>
    public List<LiveTag>? AvailableTags => Children is { Count: > 0 } children ? children : null;

    /// <summary>
    /// 当前没有可用的子分区（加载失败或该分区没有标签）.
    /// </summary>
    public bool HasNoChildPartitions => AvailableTags is null;

    [ObservableProperty]
    public partial bool IsLiveLoading { get; set; }

    [ObservableProperty]
    public partial LiveTag CurrentTag { get; set; }

    /// <summary>
    /// 初始化完成.
    /// </summary>
    public event EventHandler Initialized;

    /// <summary>
    /// 直播列表已完成更新.
    /// </summary>
    public event EventHandler LiveListUpdated;

    /// <summary>
    /// 显示的直播间列表.
    /// </summary>
    public ObservableCollection<LiveItemViewModel> Rooms { get; } = new();
}
