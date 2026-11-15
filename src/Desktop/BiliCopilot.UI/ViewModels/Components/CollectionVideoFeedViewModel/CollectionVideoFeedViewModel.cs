// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.User;
using BiliCopilot.UI.ViewModels.View;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 合集或系列内视频的视图模型：按页加载，追加到同一份列表.
/// </summary>
public sealed partial class CollectionVideoFeedViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionVideoFeedViewModel"/> class.
    /// </summary>
    /// <param name="userService">用户服务.</param>
    /// <param name="logger">日志.</param>
    public CollectionVideoFeedViewModel(IUserService userService, ILogger logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// 取消指定代次的加载.
    /// </summary>
    /// <param name="generation">页面记录下的加载代次.</param>
    public void CancelLoad(int generation)
    {
        CancellationTokenSource? cancellation = null;
        var cancelled = false;
        lock (_loadLock)
        {
            if (_loadGeneration == generation)
            {
                cancellation = _loadCancellation;
                _loadCancellation = null;
                _loadGeneration++;
                cancelled = true;
            }
        }

        if (!cancelled)
        {
            return;
        }

        CancelAndDispose(cancellation);
        IsLoading = false;
        IsInitialLoading = false;
        IsLoadingMore = false;
    }

    /// <summary>
    /// 切换到新的合集.
    /// </summary>
    /// <param name="parameter">合集参数.</param>
    [RelayCommand]
    private void Reset(CollectionDetailParameter parameter)
    {
        Items.Clear();
        _parameter = parameter;
        _page = 0;
        _total = 0;
        _preventLoadMore = false;
        IsEmpty = false;
        IsLoading = false;
        IsInitialLoading = false;
        IsLoadingMore = false;
        ResetLoadState();
    }

    /// <summary>
    /// 加载下一页.
    /// </summary>
    /// <returns>加载任务.</returns>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadItemsAsync()
    {
        if (_parameter is null || IsLoading || _preventLoadMore)
        {
            return;
        }

        var (generation, cancellation) = BeginLoad();
        var parameter = _parameter;
        var isFirstPage = _page == 0;
        try
        {
            IsLoading = true;
            IsInitialLoading = isFirstPage;
            IsLoadingMore = !isFirstPage;

            var (videos, total) = await _userService.GetCollectionVideosAsync(parameter.UserId, parameter.CollectionId, parameter.IsSeason, _page, cancellation.Token);
            if (!IsCurrentLoad(generation))
            {
                return;
            }

            _total = total;
            foreach (var video in videos)
            {
                Items.Add(new VideoItemViewModel(video, VideoCardStyle.Search));
            }

            _page++;
            _preventLoadMore = videos.Count == 0 || (_total > 0 && Items.Count >= _total);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // 目标已切换，结果作废.
        }
        catch (Exception ex)
        {
            if (IsCurrentLoad(generation))
            {
                _preventLoadMore = true;
                _logger.LogError(ex, "加载合集视频失败.");
            }
        }
        finally
        {
            cancellation.Dispose();
            lock (_loadLock)
            {
                if (ReferenceEquals(_loadCancellation, cancellation))
                {
                    _loadCancellation = null;
                }
            }

            if (IsCurrentLoad(generation))
            {
                IsEmpty = Items.Count == 0;
                IsLoading = false;
                IsInitialLoading = false;
                IsLoadingMore = false;
            }
        }
    }

    /// <summary>
    /// 重新加载第一页.
    /// </summary>
    /// <returns>加载任务.</returns>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync()
    {
        Items.Clear();
        _page = 0;
        _total = 0;
        _preventLoadMore = false;
        IsEmpty = false;
        ResetLoadState();
        await LoadItemsAsync();
    }

    private (int Generation, CancellationTokenSource Cancellation) BeginLoad()
    {
        var cancellation = new CancellationTokenSource();
        CancellationTokenSource? previous;
        int generation;
        lock (_loadLock)
        {
            generation = ++_loadGeneration;
            previous = _loadCancellation;
            _loadCancellation = cancellation;
        }

        CancelAndDispose(previous);
        return (generation, cancellation);
    }

    private bool IsCurrentLoad(int generation)
    {
        lock (_loadLock)
        {
            return generation == _loadGeneration;
        }
    }

    private void ResetLoadState()
    {
        CancellationTokenSource? previous;
        lock (_loadLock)
        {
            _loadGeneration++;
            previous = _loadCancellation;
            _loadCancellation = null;
        }

        CancelAndDispose(previous);
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
        finally
        {
            cancellation.Dispose();
        }
    }
}
