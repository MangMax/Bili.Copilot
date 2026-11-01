// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Models.Article;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 文章阅读器页面视图模型.
/// </summary>
public sealed partial class ArticleReaderPageViewModel
{
    private readonly IArticleDiscoveryService _discoveryService;
    private readonly IArticleOperationService _operationService;
    private readonly ILogger<ArticleReaderPageViewModel> _logger;

    /// <summary>
    /// 文章内容加载的代次，每次初始化递增.
    /// </summary>
    /// <remarks>
    /// 阅读器视图模型是单例，快速切换文章时可能有多次加载并行。旧代次的
    /// 响应不得写入标题、作者、头像、正文或加载态。
    /// </remarks>
    private int _contentGeneration;

    private readonly object _loadLock = new();

    private CancellationTokenSource? _contentCancellation;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string? _author;

    [ObservableProperty]
    private Uri? _avatar;

    [ObservableProperty]
    private bool _isVip;

    [ObservableProperty]
    private bool _isFollowed;

    [ObservableProperty]
    private bool? _isLiked;

    [ObservableProperty]
    private bool? _isFavorited;

    [ObservableProperty]
    private int _likeCount;

    [ObservableProperty]
    private int _commentCount;

    [ObservableProperty]
    private int _favoriteCount;

    [ObservableProperty]
    private bool _isCommentsOpened;

    [ObservableProperty]
    private bool _isAIOverlayOpened;

    /// <summary>
    /// 文章已加载.
    /// </summary>
    public event EventHandler ArticleInitialized;

    /// <summary>
    /// 文章标识.
    /// </summary>
    public ArticleIdentifier? Article { get; private set; }

    /// <summary>
    /// 当前内容加载的代次；页面卸载时用它只取消本页面发起的加载.
    /// </summary>
    public int CurrentLoadGeneration
    {
        get
        {
            lock (_loadLock)
            {
                return _contentGeneration;
            }
        }
    }

    /// <summary>
    /// 文章内容.
    /// </summary>
    public ArticleDetail? Content { get; private set; }

    /// <summary>
    /// 评论模块.
    /// </summary>
    public CommentMainViewModel CommentModule { get; }

    /// <summary>
    /// AI 模块.
    /// </summary>
    public AIViewModel AI { get; }
}
