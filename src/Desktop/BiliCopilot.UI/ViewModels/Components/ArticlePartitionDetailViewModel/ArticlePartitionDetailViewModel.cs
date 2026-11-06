// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Article;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 专栏文章分区详情视图模型.
/// </summary>
public sealed partial class ArticlePartitionDetailViewModel : ViewModelBase<PartitionViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticlePartitionDetailViewModel"/> class.
    /// </summary>
    public ArticlePartitionDetailViewModel(
        PartitionViewModel data,
        IArticleDiscoveryService service)
        : base(data)
    {
        _service = service;
        _logger = this.Get<ILogger<ArticlePartitionDetailViewModel>>();
        _isRecommendPartition = data.Data.Id == "_";
        _isHotPartition = data.Data.Id == "-";
    }

    [RelayCommand]
    private void Initialize()
    {
        if (Articles.Count > 0)
        {
            return;
        }

        if (Data.Children is not null)
        {
            var children = Data.Children.ToList();
            var rcmdPartition = new Partition(Data.Data.Id, ResourceToolkit.GetLocalizedString(StringNames.RecommendArticle));
            children.Insert(0, new PartitionViewModel(rcmdPartition));
            Children = [.. children];
        }

        if (_isRecommendPartition || _isHotPartition)
        {
            IsRecommend = true;
            ChangeChildPartition(Data);
        }
        else
        {
            SortTypes = [.. Enum.GetValues<ArticleSortType>()];
            SelectedSortType = ArticleSortType.Default;
            ChangeChildPartition(Children.First());
        }

        Initialized?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ChangeSortType(ArticleSortType sortType)
    {
        if (IsRecommend)
        {
            return;
        }

        SelectedSortType = sortType;
        SupersedePendingLoad();

        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            SupersedePendingLoad();
            CacheDisplayedArticles();
            _childPartitionArticleCache.Clear();
            Articles.Clear();
            _displayedPartition = CurrentPartition;
            _childPartitionOffsetCache.Clear();
            foreach (var partition in Children.Where(p => p.Data.Id != Data.Data.Id))
            {
                _stoppedPartitions.Remove(partition.Data.Id);
                _noProgressPages.Remove(partition.Data.Id);
            }
            await LoadArticlesAsync();
        });
    }

    [RelayCommand]
    private void Refresh()
    {
        SupersedePendingLoad();

        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            // 刷新必须以当前列表为准：清空集合的同时作废清空前发起的加载，
            // 否则旧分页结果会在清空后追加回来，刷新看起来“没有生效”。
            SupersedePendingLoad();
            CacheDisplayedArticles();
            if (IsRecommend)
            {
                _recommendOffset = 0;
                _recommendCache?.Clear();
            }
            else
            {
                _childPartitionArticleCache.Remove(CurrentPartition.Data.Id);
                _childPartitionOffsetCache.Remove(CurrentPartition.Data.Id);
            }

            _stoppedPartitions.Remove(CurrentPartition.Data.Id);
            _noProgressPages.Remove(CurrentPartition.Data.Id);
            Articles.Clear();
            _displayedPartition = CurrentPartition;
            await LoadArticlesAsync();
        });
    }

    [RelayCommand]
    private void ChangeChildPartition(PartitionViewModel partition)
    {
        if (partition is null || partition.Data.Equals(CurrentPartition?.Data))
        {
            return;
        }

        IsRecommend = partition.Data.Id == Data.Data.Id;
        CurrentPartition = partition;
        SupersedePendingLoad();
        var generation = _listGeneration;

        this.Get<DispatcherQueue>().TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            // 已被后续选择或刷新替代的切换不能清空列表、作废新请求或重复补屏。
            if (generation != _listGeneration)
            {
                return;
            }

            SupersedePendingLoad();
            CacheDisplayedArticles();
            Articles.Clear();
            _displayedPartition = partition;
            if (IsRecommend && _recommendCache?.Count > 0)
            {
                foreach (var item in _recommendCache)
                {
                    Articles.Add(item);
                }
            }
            else if (_childPartitionArticleCache.TryGetValue(CurrentPartition.Data.Id, out var cache))
            {
                foreach (var item in cache)
                {
                    Articles.Add(item);
                }
            }

            if (Articles.Count == 0)
            {
                await LoadArticlesAsync();
            }
            else
            {
                ArticleListUpdated?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void CacheDisplayedArticles()
    {
        if (Articles.Count == 0 || _displayedPartition is null)
        {
            return;
        }

        if (_displayedPartition.Data.Id == Data.Data.Id)
        {
            _recommendCache = Articles.ToList();
        }
        else
        {
            _childPartitionArticleCache[_displayedPartition.Data.Id] = Articles.ToList();
        }
    }

    [RelayCommand]
    private async Task LoadArticlesAsync()
    {
        if (IsArticleLoading || CurrentPartition is null
            || _displayedPartition?.Data.Id != CurrentPartition.Data.Id
            || _stoppedPartitions.Contains(CurrentPartition.Data.Id))
        {
            return;
        }

        IsArticleLoading = true;
        var generation = _listGeneration;
        if (_isRecommendPartition)
        {
            await LoadRecommendPartitionArticlesAsync(generation);
        }
        else if (_isHotPartition)
        {
            await LoadHotPartitionArticlesAsync(generation);
        }
        else if (IsRecommend)
        {
            await LoadRecommendArticlesAsync(generation);
        }
        else
        {
            await LoadChildPartitionArticlesAsync(generation);
        }

        // 加载期间列表已被刷新、切换分区或切换排序作废时，这一批结果不能
        // 再驱动当前列表的加载态或追加事件。
        if (generation != _listGeneration)
        {
            return;
        }

        IsArticleLoading = false;
        ArticleListUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 作废进行中的加载，并把加载态交还给随后发起的新请求.
    /// </summary>
    /// <remarks>
    /// 切换排序、切换子分区和刷新都会替换当前列表。旧请求返回后只能被丢弃，
    /// 不能继续追加条目、写回分页偏移或关闭新请求的加载态。
    /// </remarks>
    private void SupersedePendingLoad()
    {
        _listGeneration++;
        IsArticleLoading = false;
    }

    private async Task LoadRecommendArticlesAsync(int generation)
    {
        try
        {
            var (articles, offset) = await _service.GetPartitionArticlesAsync(CurrentPartition.Data, ArticleSortType.Default, _recommendOffset);
            if (generation != _listGeneration)
            {
                return;
            }

            ApplyPage(articles, _recommendOffset, offset);
            _recommendOffset = offset;
        }
        catch (Exception ex)
        {
            if (generation != _listGeneration)
            {
                return;
            }

            _stoppedPartitions.Add(CurrentPartition.Data.Id);
            _logger.LogError(ex, $"尝试加载 {Data.Data.Name} 分区推荐文章列表时出错.");
        }
    }

    private async Task LoadChildPartitionArticlesAsync(int generation)
    {
        try
        {
            var partitionId = CurrentPartition.Data.Id;
            var pn = 0;
            if (_childPartitionOffsetCache.TryGetValue(partitionId, out var offsetCache))
            {
                pn = offsetCache;
            }

            var (articles, nextPn) = await _service.GetPartitionArticlesAsync(CurrentPartition.Data, SelectedSortType, pn);
            if (generation != _listGeneration)
            {
                return;
            }

            ApplyPage(articles, pn, nextPn);
            _childPartitionOffsetCache[partitionId] = nextPn;
        }
        catch (Exception ex)
        {
            if (generation != _listGeneration)
            {
                return;
            }

            _stoppedPartitions.Add(CurrentPartition.Data.Id);
            _logger.LogError(ex, $"尝试加载 {Data.Data.Name}/{CurrentPartition.Data.Name} 分区文章时出错.");
        }
    }

    private async Task LoadHotPartitionArticlesAsync(int generation)
    {
        try
        {
            // 将榜单所有文章全部加载.
            var categories = await _service.GetHotCategoriesAsync();
            var articles = new List<ArticleInformation>();
            foreach (var cate in categories)
            {
                var d = await _service.GetHotArticlesAsync(cate.Key);
                articles.AddRange(d);
            }

            if (generation != _listGeneration)
            {
                return;
            }

            articles = articles.OrderByDescending(p => p.PublishDateTime).Distinct().ToList();
            TryAddArticles(articles);
        }
        catch (Exception ex)
        {
            if (generation != _listGeneration)
            {
                return;
            }

            _stoppedPartitions.Add(CurrentPartition.Data.Id);
            _logger.LogError(ex, "尝试加载热门专栏分区时出错.");
        }

        if (generation == _listGeneration)
        {
            _stoppedPartitions.Add(CurrentPartition.Data.Id);
        }
    }

    private async Task LoadRecommendPartitionArticlesAsync(int generation)
    {
        try
        {
            var (articles, _, offset) = await _service.GetRecommendArticlesAsync(_recommendOffset);
            if (generation != _listGeneration)
            {
                return;
            }

            ApplyPage(articles, _recommendOffset, offset);
            _recommendOffset = offset;
        }
        catch (Exception ex)
        {
            if (generation != _listGeneration)
            {
                return;
            }

            _stoppedPartitions.Add(CurrentPartition.Data.Id);
            _logger.LogError(ex, "尝试加载推荐文章时出错.");
        }
    }

    private void ApplyPage(IReadOnlyList<ArticleInformation> articles, int previousPage, int nextPage)
    {
        var partitionId = CurrentPartition.Data.Id;
        var previousCount = Articles.Count;
        TryAddArticles(articles);
        var noProgress = Articles.Count == previousCount
            ? _noProgressPages.GetValueOrDefault(partitionId) + 1
            : 0;
        _noProgressPages[partitionId] = noProgress;

        // 服务的页号由客户端递增，不是服务端的 has-more 标记；短页也不代表结束。
        // 允许一次跨页重叠，但连续两页没有新条目时暂停补屏，用户可刷新恢复。
        if (articles.Count == 0 || nextPage <= previousPage || noProgress >= 2)
        {
            _stoppedPartitions.Add(partitionId);
        }
    }

    private void TryAddArticles(IReadOnlyList<ArticleInformation> articles)
    {
        if (articles is not null)
        {
            foreach (var item in articles)
            {
                if (Articles.Any(p => p.Data.Equals(item)))
                {
                    continue;
                }

                Articles.Add(new ArticleItemViewModel(item, ArticleCardStyle.Partition));
            }
        }
    }
}
