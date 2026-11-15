// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 合集详情页视图模型.
/// </summary>
public sealed partial class CollectionDetailPageViewModel
{
    /// <summary>
    /// 合集内的视频列表.
    /// </summary>
    public CollectionVideoFeedViewModel Feed { get; }

    /// <summary>
    /// 标题.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; }

    /// <summary>
    /// 视频数量文案.
    /// </summary>
    [ObservableProperty]
    public partial string CountText { get; set; }

    /// <summary>
    /// 封面.
    /// </summary>
    [ObservableProperty]
    public partial Uri? Cover { get; set; }
}
