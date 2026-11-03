// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 娱乐索引视图模型.
/// </summary>
public sealed partial class EntertainmentIndexViewModel
{
    private readonly IEntertainmentDiscoveryService _service;
    private readonly ILogger<EntertainmentIndexViewModel> _logger;
    private int _pageNumer;

    [ObservableProperty]
    public partial List<IndexFilterViewModel>? Filters { get; set; }

    [ObservableProperty]
    public partial bool IsFilterLoading { get; set; }

    [ObservableProperty]
    public partial bool IsSeasonLoading { get; set; }

    /// <summary>
    /// 条目已更新.
    /// </summary>
    public event EventHandler ItemsUpdated;

    /// <inheritdoc/>
    public PgcSectionType SectionType { get; }

    /// <summary>
    /// 剧集列表.
    /// </summary>
    public ObservableCollection<SeasonItemViewModel> Items { get; } = new();
}
