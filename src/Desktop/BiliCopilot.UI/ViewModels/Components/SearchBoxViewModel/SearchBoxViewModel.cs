// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Search;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 搜索视图模型.
/// </summary>
public sealed partial class SearchBoxViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchBoxViewModel"/> class.
    /// </summary>
    public SearchBoxViewModel(
        ISearchService searchService,
        ILogger<SearchBoxViewModel> logger,
        TimeProvider? timeProvider = null)
    {
        _searchService = searchService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 设置附加搜索区域（用于在特殊页面进行局部搜索）.
    /// </summary>
    public void SetExtraRegion(string regionId, string regionName)
    {
        TryCancelSuggest();
        Suggestion.Clear();
        _extraRegionId = regionId;
        _extraRegionName = regionName;
    }

    partial void OnKeywordChanged(string value)
    {
        TryCancelSuggest();
        Suggestion.Clear();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task DebounceSuggestionsAsync() => LoadSuggestionsAsync(debounce: true);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ReloadSuggestionsAsync() => LoadSuggestionsAsync(debounce: false);

    // Called on the UI thread. Each operation owns its token source until its await completes;
    // invalidation immediately revokes publication, even when a service ignores cancellation.
    private async Task LoadSuggestionsAsync(bool debounce)
    {
        TryCancelSuggest();
        using var source = new CancellationTokenSource();
        _cancellationTokenSource = source;
        var token = source.Token;
        var keyword = Keyword;
        try
        {
            if (debounce)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), _timeProvider, token);
                token.ThrowIfCancellationRequested();
            }

            if (!string.IsNullOrEmpty(_extraRegionId))
            {
                Suggestion.Clear();
                Suggestion.Add(new("global", ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.WholePartitions)));
                Suggestion.Add(new(_extraRegionId, _extraRegionName));
                foreach (var item in Suggestion)
                {
                    item.SearchContent = keyword;
                    item.Keyword = keyword;
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                if (!SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.ShowSearchRecommend, true))
                {
                    _recommendItems.Clear();
                    Suggestion.Clear();
                    return;
                }

                if (_recommendItems.Count == 0)
                {
                    var recommends = await _searchService.GetSearchRecommendsAsync(token);
                    if (!ReferenceEquals(_cancellationTokenSource, source))
                    {
                        return;
                    }

                    _recommendItems.AddRange(recommends);
                }

                Suggestion.Clear();
                foreach (var item in _recommendItems)
                {
                    Suggestion.Add(new(new Richasy.BiliKernel.Models.Search.SearchSuggestItem(item.Text, item.Keyword)));
                }
            }
            else
            {
                Suggestion.Clear();
                var suggests = await _searchService.GetSearchSuggestsAsync(keyword, token);
                if (!ReferenceEquals(_cancellationTokenSource, source))
                {
                    return;
                }

                if (suggests is not null)
                {
                    foreach (var item in suggests)
                    {
                        Suggestion.Add(new(item));
                    }
                }
            }
        }
        catch (Exception ex) when (token.IsCancellationRequested && ex.GetBaseException() is OperationCanceledException)
        {
            // BiliHttpClient can wrap cancellation in KernelException. Real failures still log below.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载搜索建议时出错.");
        }
        finally
        {
            if (ReferenceEquals(_cancellationTokenSource, source))
            {
                _cancellationTokenSource = null;
            }
        }
    }

    [RelayCommand]
    private async Task LoadHotSearchAsync()
    {
        if (HotSearchItems.Count > 0)
        {
            return;
        }

        IsHotSearchLoading = true;
        try
        {
            var hotSearches = await _searchService.GetTotalHotSearchAsync(30);
            foreach (var item in hotSearches)
            {
                HotSearchItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载热搜榜单时出错.");
        }

        IsHotSearchLoading = false;
    }

    [RelayCommand]
    private void TryCancelSuggest()
    {
        var source = _cancellationTokenSource;
        _cancellationTokenSource = null;
        source?.Cancel();
    }

    [RelayCommand]
    private void Search(SearchSuggestItemViewModel vm)
    {
        var keyword = vm.Keyword;
        if (string.IsNullOrEmpty(keyword))
        {
            return;
        }

        if (string.IsNullOrEmpty(vm.RegionId) || vm.RegionId == "global")
        {
            this.Get<NavigationViewModel>().Search(keyword);
        }
        else
        {
            this.Get<NavigationViewModel>().SearchInRegion(keyword);
        }
    }
}
