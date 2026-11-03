// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Models.User;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 用户动态详情视图模型.
/// </summary>
public sealed partial class UserMomentDetailViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserMomentDetailViewModel"/> class.
    /// </summary>
    public UserMomentDetailViewModel(
        IMomentDiscoveryService service,
        ILogger<UserMomentDetailViewModel> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 设置是否为视频动态.
    /// </summary>
    public void SetIsVideo(bool isVideo)
    {
        _isVideo = isVideo;
        Title = isVideo ? ResourceToolkit.GetLocalizedString(StringNames.Video) : ResourceToolkit.GetLocalizedString(StringNames.Comprehensive);
    }

    /// <summary>
    /// 设置显示评论的动作.
    /// </summary>
    public void SetShowCommentAction(Action<MomentItemViewModel> action)
        => _showCommentAction = action;

    /// <summary>
    /// 是否为视频动态模块.
    /// </summary>
    /// <returns>结果.</returns>
    public bool IsVideo()
        => _isVideo;

    /// <summary>
    /// 取消指定代次的加载；页面失活时用它只取消该页面发起的那一代.
    /// </summary>
    /// <param name="generation">页面记录下的加载代次.</param>
    public void CancelLoad(int generation)
    {
        CancellationTokenSource? cancellation = null;
        var cancelledGeneration = 0;
        lock (_loadLock)
        {
            if (_loadGeneration == generation)
            {
                cancellation = _loadCancellation;
                _loadCancellation = null;

                // 递增代次，使已经在途的旧成功/异常也无法落地.
                cancelledGeneration = ++_loadGeneration;
            }
        }

        CancelAndDispose(cancellation);
        if (cancelledGeneration != 0 && IsCurrentLoad(cancelledGeneration))
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Reset(UserProfile profile)
    {
        Clear();
        _user = profile;

        // 重置目标时同时重置加载态与代次，而不是只清数据：
        // 上一目标的在途响应被作废，它的 IsLoading 也不会把新目标的加载挡掉。
        ResetLoadState();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InitializeAsync()
    {
        if (Items.Count > 0 || _preventLoadMore)
        {
            return;
        }

        await LoadItemsAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync()
    {
        Clear();
        await LoadItemsAsync();
    }

    [RelayCommand]
    private void Clear()
    {
        _offset = default;
        IsEmpty = false;
        _preventLoadMore = false;
        Items = new System.Collections.ObjectModel.ObservableCollection<MomentItemViewModel>();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadItemsAsync()
    {
        if (IsEmpty || IsLoading || _preventLoadMore)
        {
            return;
        }

        // 动态分区挂在参数化页面上：每次加载记下代次并取消上一代，
        // 只有当前代次的结果可以写入列表、分页游标与加载态。
        var (generation, cancellation) = BeginLoad();
        var user = _user;
        try
        {
            IsLoading = true;
            var (moments, offset, hasMore) = _isVideo
                ? await _service.GetUserVideoMomentsAsync(user, _offset, cancellation.Token)
                : await _service.GetUserComprehensiveMomentsAsync(user, _offset, cancellation.Token);
            if (!IsCurrentLoad(generation))
            {
                return;
            }

            _offset = offset;
            _preventLoadMore = !hasMore || string.IsNullOrEmpty(offset);
            if (moments is not null)
            {
                foreach (var item in moments.Select(p => new MomentItemViewModel(p, _isVideo ? MomentCardStyle.Video : MomentCardStyle.Comprehensive, _showCommentAction)))
                {
                    Items.Add(item);
                }

                ListUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // 切换目标或离开页面已取消本次加载，不写入也不记录为失败.
        }
        catch (Exception ex)
        {
            if (IsCurrentLoad(generation))
            {
                _preventLoadMore = true;
                _logger.LogError(ex, "加载动态列表失败.");
            }
        }
        finally
        {
            cancellation.Dispose();
            lock (_loadLock)
            {
                // 已释放的令牌不再留在字段里，避免后续取消依赖 ObjectDisposedException 兜底。
                if (ReferenceEquals(_loadCancellation, cancellation))
                {
                    _loadCancellation = null;
                }
            }

            if (IsCurrentLoad(generation))
            {
                IsEmpty = Items.Count == 0;
                IsLoading = false;
            }
        }
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

        // 先替换当前代次，再在锁外取消旧请求，避免取消回调重入时持锁。
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
        IsLoading = false;
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
