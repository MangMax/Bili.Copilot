// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Models.User;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 用户动态详情视图模型.
/// </summary>
public sealed partial class UserMomentDetailViewModel
{
    private readonly IMomentDiscoveryService _service;
    private readonly ILogger<UserMomentDetailViewModel> _logger;
    private bool _isVideo;
    private bool _preventLoadMore;
    private string? _offset;
    private UserProfile _user;
    private Action<MomentItemViewModel> _showCommentAction;

    /// <summary>
    /// 动态列表的加载代次，每次重新加载递增.
    /// </summary>
    /// <remarks>
    /// 用户空间是参数化页面，两个动态分区被同一份视图模型复用。旧目标的
    /// 响应不得写入列表、分页游标或加载态。
    /// </remarks>
    private int _loadGeneration;

    private readonly object _loadLock = new();

    private CancellationTokenSource? _loadCancellation;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private ObservableCollection<MomentItemViewModel> _items = new();

    /// <summary>
    /// 列表已完成更新.
    /// </summary>
    public event EventHandler ListUpdated;

    /// <summary>
    /// 当前加载的代次；页面卸载时用它只取消本页面发起的那一代加载.
    /// </summary>
    public int CurrentLoadGeneration
    {
        get
        {
            lock (_loadLock)
            {
                return _loadGeneration;
            }
        }
    }
}
