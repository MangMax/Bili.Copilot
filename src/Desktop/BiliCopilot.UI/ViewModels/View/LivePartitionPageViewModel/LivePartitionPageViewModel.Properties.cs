// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 直播分区页面视图模型.
/// </summary>
public sealed partial class LivePartitionPageViewModel
{
    private readonly ILiveDiscoveryService _service;
    private readonly ILogger<LivePartitionPageViewModel> _logger;
    private readonly Dictionary<PartitionViewModel, LivePartitionDetailViewModel> _partitionCache = new();
    private readonly Dictionary<string, string> _partitionSelectedCache = new();

    private int _recommendOffset;

    [ObservableProperty]
    public partial double SubNavColumnWidth { get; set; }

    [ObservableProperty]
    public partial bool IsSubNavEnabled { get; set; }

    [ObservableProperty]
    public partial List<PartitionViewModel>? SubPartitions { get; set; }

    [ObservableProperty]
    public partial bool IsSectionLoading { get; set; }

    [ObservableProperty]
    public partial bool IsRecommendLoading { get; set; }

    [ObservableProperty]
    public partial bool IsFollowRoomsEmpty { get; set; }

    [ObservableProperty]
    public partial PartitionViewModel SelectedMainSection { get; set; }

    [ObservableProperty]
    public partial PartitionViewModel SelectedSubSection { get; set; }

    [ObservableProperty]
    public partial LivePartitionDetailViewModel SelectedDetail { get; set; }

    /// <summary>
    /// 分区加载完成.
    /// </summary>
    public event EventHandler SectionInitialized;

    /// <summary>
    /// 主分区变更.
    /// </summary>
    public event EventHandler MainSectionChanged;

    /// <summary>
    /// 推荐直播间更新.
    /// </summary>
    public event EventHandler RecommendUpdated;

    /// <summary>
    /// 直播间主分区.
    /// </summary>
    public ObservableCollection<PartitionViewModel> MainSections { get; } = new();

    /// <summary>
    /// 推荐直播间.
    /// </summary>
    public ObservableCollection<LiveItemViewModel> RecommendRooms { get; } = new();

    /// <summary>
    /// 关注的直播间.
    /// </summary>
    public ObservableCollection<LiveItemViewModel> FollowRooms { get; } = new();
}
