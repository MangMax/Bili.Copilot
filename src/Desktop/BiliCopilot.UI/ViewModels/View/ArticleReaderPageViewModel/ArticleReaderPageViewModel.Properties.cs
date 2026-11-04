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
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial bool HasLoadFailed { get; set; }

    [ObservableProperty]
    public partial bool HasContent { get; set; }

    /// <summary>
    /// 重试后通知页面更新其拥有的加载代次.
    /// </summary>
    public event Action<int, int>? LoadRetried;

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string? Author { get; set; }

    [ObservableProperty]
    public partial Uri? Avatar { get; set; }

    [ObservableProperty]
    public partial bool IsVip { get; set; }

    [ObservableProperty]
    public partial bool IsFollowed { get; set; }

    [ObservableProperty]
    public partial bool? IsLiked { get; set; }

    [ObservableProperty]
    public partial bool? IsFavorited { get; set; }

    [ObservableProperty]
    public partial int LikeCount { get; set; }

    [ObservableProperty]
    public partial int CommentCount { get; set; }

    [ObservableProperty]
    public partial int FavoriteCount { get; set; }

    [ObservableProperty]
    public partial bool IsCommentsOpened { get; set; }

    [ObservableProperty]
    public partial bool IsAIOverlayOpened { get; set; }

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
