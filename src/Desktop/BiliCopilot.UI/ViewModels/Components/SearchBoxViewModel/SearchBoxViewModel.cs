// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Services.Search;
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
        CancelPendingSubmission();
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
        var keyword = Keyword;
        if (BiliSearchLink.Parse(keyword) is not null)
        {
            Suggestion.Clear();
            return;
        }

        using var source = new CancellationTokenSource();
        _cancellationTokenSource = source;
        var token = source.Token;
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
        catch (Exception ex) when (token.IsCancellationRequested
            && (ex is OperationCanceledException || ex.GetBaseException() is OperationCanceledException))
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

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task SearchAsync(SearchSuggestItemViewModel vm)
        => SubmitSearchAsync(null, BeginSubmission(), vm);

    internal void CancelPendingSubmission()
    {
        var source = _pendingLinkSubmission;
        _pendingLinkSubmission = null;
        source?.Cancel();
    }

    /// <summary>Record input before UI suppression or navigation can fail.</summary>
    public Guid BeginSubmission()
    {
        var id = Guid.NewGuid();
        RecordSubmission(id, "submitted");
        return id;
    }

    internal void RecordSubmission(Guid id, string stage)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("SearchSubmission {SubmissionId} {Stage}", id, stage);
        }
    }

    /// <summary>Submit using the identity captured at the input event.</summary>
    public async Task SubmitSearchAsync(string? text, Guid submissionId, SearchSuggestItemViewModel? selection = null)
    {
        CancelPendingSubmission();
        RecordSubmission(submissionId, "command");
        var keyword = selection is null ? text : selection.Keyword;
        if (string.IsNullOrEmpty(keyword))
        {
            RecordSubmission(submissionId, "empty");
            return;
        }

        if (selection is null && BiliSearchLink.Parse(keyword) is { } link)
        {
            await OpenLinkAsync(link, submissionId);
        }
        else if (string.IsNullOrEmpty(selection?.RegionId) || selection.RegionId == "global")
        {
            this.Get<NavigationViewModel>().Search(keyword, submissionId);
        }
        else
        {
            RecordSubmission(submissionId, "region");
            this.Get<NavigationViewModel>().SearchInRegion(keyword);
        }
    }

    private async Task OpenLinkAsync(BiliSearchLink link, Guid submissionId)
    {
        var navigator = this.Get<ISearchLinkNavigator>();
        using var source = new CancellationTokenSource();
        _pendingLinkSubmission = source;
        try
        {
            RecordSubmission(submissionId, "link");
            var target = link.Kind == BiliSearchLinkKind.Short
                ? await this.Get<SearchLinkResolver>().ResolveAsync(link, source.Token)
                : link;

            source.Token.ThrowIfCancellationRequested();
            if (target is not null)
            {
                await navigator.OpenAsync(target);
                return;
            }
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("打开搜索框链接失败 ({ErrorType})", ex.GetType().Name);
        }
        finally
        {
            if (ReferenceEquals(_pendingLinkSubmission, source))
            {
                _pendingLinkSubmission = null;
            }
        }

        if (!source.IsCancellationRequested)
        {
            RecordSubmission(submissionId, "link-failed");
            navigator.ShowFailure();
        }
    }
}
