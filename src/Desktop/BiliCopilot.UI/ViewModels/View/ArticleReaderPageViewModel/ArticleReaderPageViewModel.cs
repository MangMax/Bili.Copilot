// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Models.Article;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 文章阅读器页面视图模型.
/// </summary>
public sealed partial class ArticleReaderPageViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleReaderPageViewModel"/> class.
    /// </summary>
    public ArticleReaderPageViewModel(
        IArticleDiscoveryService discovery,
        IArticleOperationService operation,
        ILogger<ArticleReaderPageViewModel> logger,
        CommentMainViewModel comment,
        AIViewModel ai)
    {
        _discoveryService = discovery;
        _operationService = operation;
        _logger = logger;
        CommentModule = comment;
        AI = ai;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InitializeAsync(ArticleIdentifier article)
    {
        // 阅读器视图模型是单例，快速连续打开不同文章时会有多次加载并行。
        // 每次加载记下代次并取消上一次请求，只有最后一次的结果可以写入页面，
        // 避免旧请求在新文章已经打开后仍继续读取或把状态改回去。
        var (generation, cancellation) = BeginLoad();
        HasLoadFailed = false;
        HasContent = false;
        Title = article.Title;
        Content = default;
        IsLoading = true;
        Article = article;
        IsCommentsOpened = false;
        IsAIOverlayOpened = false;

        // 新文章开始加载时先清空上一篇的作者、头像与统计，
        // 否则新文章加载失败时会继续显示上一篇的身份信息。
        Author = default;
        Avatar = default;
        IsVip = false;
        IsFollowed = false;
        IsLiked = default;
        IsFavorited = default;
        LikeCount = 0;
        CommentCount = 0;
        FavoriteCount = 0;
        CommentModule.Initialize(article.Id, Richasy.BiliKernel.Models.CommentTargetType.Article, Richasy.BiliKernel.Models.CommentSortType.Hot);
        try
        {
            var content = await _discoveryService.GetArticleContentAsync(article, cancellation.Token);
            if (!IsCurrentLoad(generation))
            {
                return;
            }

            Content = content ?? throw new InvalidOperationException("Article content is unavailable.");
            HasContent = true;
            AI.InjectArticleAsync(Content);
            InitializeUser();
            InitializeStats();
            ArticleInitialized?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // 新文章或离开阅读器已取消本次读取，不写入也不记录为失败。
        }
        catch (Exception ex)
        {
            if (IsCurrentLoad(generation))
            {
                HasLoadFailed = true;
                _logger.LogError(ex, "加载文章内容时失败 (ArticleId: {ArticleId}, LoadGeneration: {LoadGeneration})", article.Id, generation);
            }
        }
        finally
        {
            cancellation.Dispose();
            if (IsCurrentLoad(generation))
            {
                IsLoading = false;
            }
        }
    }

    private bool CanRetry() => HasLoadFailed && !IsLoading && Article is not null;

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private async Task RetryAsync()
    {
        // 命令入口也守卫，防止程序直接执行时产生重复请求。
        if (!CanRetry())
        {
            return;
        }

        var previous = CurrentLoadGeneration;
        var task = InitializeAsync(Article!.Value);
        LoadRetried?.Invoke(previous, CurrentLoadGeneration);
        await task;
    }

    /// <summary>
    /// 取消指定代次的内容加载；页面卸载时用它只取消该页面发起的加载.
    /// </summary>
    /// <param name="generation">页面记录下的加载代次.</param>
    public void CancelLoad(int generation)
    {
        CancellationTokenSource? cancellation = null;
        var cancelledGeneration = 0;
        lock (_loadLock)
        {
            if (_contentGeneration == generation)
            {
                cancellation = _contentCancellation;
                _contentCancellation = null;

                // 递增代次，使已经在途的旧成功/异常也无法落地.
                cancelledGeneration = ++_contentGeneration;
            }
        }

        CancelAndDispose(cancellation);
        if (cancelledGeneration != 0 && IsCurrentLoad(cancelledGeneration))
        {
            IsLoading = false;
        }
    }

    private (int Generation, CancellationTokenSource Cancellation) BeginLoad()
    {
        var cancellation = new CancellationTokenSource();
        CancellationTokenSource? previous;
        int generation;
        lock (_loadLock)
        {
            generation = ++_contentGeneration;
            previous = _contentCancellation;
            _contentCancellation = cancellation;
        }

        // 先替换当前代次，再在锁外取消旧请求，避免取消回调重入时持锁。
        CancelAndDispose(previous);
        return (generation, cancellation);
    }

    private bool IsCurrentLoad(int generation)
    {
        lock (_loadLock)
        {
            return generation == _contentGeneration;
        }
    }

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 旧加载已自行释放，取消是幂等的.
        }

        cancellation.Dispose();
    }

    [RelayCommand]
    private async Task ToggleLikeAsync()
    {
        if (Article is null || IsLiked is not bool currentState)
        {
            return;
        }

        var state = !currentState;
        try
        {
            await _operationService.ToggleLikeAsync(Article.Value, state);
            IsLiked = state;
            if (IsLiked ?? false)
            {
                LikeCount++;
            }
            else
            {
                LikeCount--;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换点赞状态时失败");
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (Article is null || IsFavorited is not bool currentState)
        {
            return;
        }

        var state = !currentState;
        try
        {
            await _operationService.ToggleFavoriteAsync(Article.Value, state);
            IsFavorited = state;
            if (IsFavorited ?? false)
            {
                FavoriteCount++;
            }
            else
            {
                FavoriteCount--;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换收藏状态时失败");
        }
    }

    [RelayCommand]
    private async Task ToggleCommentsAsync()
    {
        IsCommentsOpened = !IsCommentsOpened;

        if (IsCommentsOpened && CommentModule.Comments.Count == 0)
        {
            await CommentModule.LoadItemsCommand.ExecuteAsync(default);
        }
    }

    [RelayCommand]
    private void ShowUserSpace()
        => this.Get<NavigationViewModel>().NavigateToOver(typeof(UserSpacePage), Content.Author.Profile.User);

    [RelayCommand]
    private void ToggleAIOverlayVisibility()
    {
        IsAIOverlayOpened = !IsAIOverlayOpened;
        if (IsAIOverlayOpened)
        {
            AI.InitializeCommand.Execute(default);
        }
        else
        {
            AI.ClearCommand.Execute(default);
        }
    }

    private void InitializeUser()
    {
        if (Content is null)
        {
            return;
        }

        Author = Content.Author.Profile.User.Name;
        Avatar = Content.Author.Profile.User.Avatar.Uri;
        IsVip = Content.Author.Profile.IsVip ?? false;
        IsFollowed = Content.Author.Community.Relation == Richasy.BiliKernel.Models.User.UserRelationStatus.Following;
    }

    private void InitializeStats()
    {
        if (Content is null)
        {
            return;
        }

        IsLiked = Content.IsLiked;
        IsFavorited = Content.IsFavorited;
        LikeCount = Content.CommunityInformation.LikeCount ?? 0;
        CommentCount = Content.CommunityInformation.CommentCount ?? 0;
        FavoriteCount = Content.CommunityInformation.FavoriteCount ?? 0;
    }
}
