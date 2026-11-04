// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Search;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Search;
using System.Diagnostics;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 搜索页面视图模型.
/// </summary>
public sealed partial class SearchPageViewModel : LayoutPageViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchPageViewModel"/> class.
    /// </summary>
    public SearchPageViewModel(
        ISearchService service,
        ILogger<SearchPageViewModel> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override string GetPageKey()
        => nameof(SearchPage);

    [RelayCommand]
    private async Task SearchAsync(object parameter)
    {
        var submission = parameter as SearchSubmission;
        if (submission is not null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("SearchSubmission {SubmissionId} page", submission.Id);
        }
        var keyword = submission?.Keyword ?? parameter as string;
        if (string.IsNullOrEmpty(keyword))
        {
            throw new ArgumentNullException(nameof(keyword));
        }

        if (keyword.StartsWith("av", StringComparison.InvariantCultureIgnoreCase))
        {
            var videoId = keyword[2..];

            // videoId should be a number.
            if (long.TryParse(videoId, out var id))
            {
                this.Get<NavigationViewModel>().Back();
                var video = new VideoInformation(new MediaIdentifier(videoId, default, default), default);
                new VideoItemViewModel(video, VideoCardStyle.Search).PlayCommand.Execute(default);
                return;
            }
        }
        else if (keyword.StartsWith("BV"))
        {
            try
            {
                var video = new VideoInformation(new MediaIdentifier(IdToolkit.Bv2Av(keyword).ToString(), default, default), default);
                this.Get<NavigationViewModel>().Back();
                new VideoItemViewModel(video, VideoCardStyle.Search).PlayCommand.Execute(default);
                return;
            }
            catch (Exception)
            {
                Debug.WriteLine("尝试解析BV号时失败.");
            }
        }

        var generation = ++_searchGeneration;
        var timer = Stopwatch.StartNew();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Search {PageId} {QueryId} accepted", _pageId, generation);
        }
        if (submission is not null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Search {PageId} {QueryId} submission {SubmissionId}", _pageId, generation, submission.Id);
        }
        SearchErrorMessage = null;
        Keyword = keyword;
        Sections = default;
        SelectedSection = default;

        try
        {
            IsSearching = true;
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Search {PageId} {QueryId} start", _pageId, generation);
            }
            var (videos, nextVideoOffset) = await _service.GetComprehensiveSearchResultAsync(keyword);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Search {PageId} {QueryId} complete {Count} {ElapsedMs}", _pageId, generation, videos.Count, timer.ElapsedMilliseconds);
            }
            if (generation != _searchGeneration)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Search {PageId} {QueryId} discard", _pageId, generation);
                }
                return;
            }

            var sections = new List<ISearchSectionDetailViewModel>
            {
                CreateVideoSection(keyword, videos, nextVideoOffset),
                CreateSection(keyword, SearchSectionType.Anime),
                CreateSection(keyword, SearchSectionType.Cinema),
                CreateSection(keyword, SearchSectionType.Live),
                CreateSection(keyword, SearchSectionType.User),
                CreateSection(keyword, SearchSectionType.Article),
            };
            Sections = [.. sections];
            SelectSection(Sections.FirstOrDefault());
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Search {PageId} {QueryId} publish", _pageId, generation);
            }
            SectionInitialized?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            if (generation == _searchGeneration && ex is not OperationCanceledException)
            {
                SetSearchError(ex);
            }
            var outcome = ex is SearchVerificationRequiredException ? "verification" : ex is OperationCanceledException ? "cancelled" : ex is HttpRequestException ? "network" : "failure";
            // Never attach the exception: its message/stack may include the query, response or credentials.
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Search {PageId} {QueryId} {Outcome} {IsCurrent} {ElapsedMs}", _pageId, generation, outcome, generation == _searchGeneration, timer.ElapsedMilliseconds);
            }
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                IsSearching = false;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanRetry), AllowConcurrentExecutions = true)]
    private async Task RetryAsync()
    {
        if (CanRetry)
        {
            await SearchAsync(Keyword);
        }
    }

    /// <summary>
    /// Invalidate pending publication when the page leaves the visual tree.
    /// </summary>
    [RelayCommand]
    private void Deactivate()
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Search {PageId} {QueryId} unloaded", _pageId, _searchGeneration);
        }
        _searchGeneration++;
        SearchErrorMessage = null;
        IsSearching = false;
    }

    /// <summary>
    /// 选择分区.
    /// </summary>
    [RelayCommand]
    private void SelectSection(ISearchSectionDetailViewModel section)
    {
        if (section is null || section == SelectedSection)
        {
            return;
        }

        SelectedSection = section;
        section.TryFirstLoadCommand.Execute(default);
    }

    private VideoSearchSectionDetailViewModel CreateVideoSection(string keyword, IReadOnlyList<VideoInformation> videos, int? page)
    {
        var generation = _searchGeneration;
        var newSection = new VideoSearchSectionDetailViewModel(_service);
        newSection.LoadFailed += (sender, error) =>
        {
            if (generation == _searchGeneration && ReferenceEquals(SelectedSection, sender) && error is not OperationCanceledException)
            {
                SetSearchError(error);
            }
        };
        newSection.Initialize(keyword, new SearchPartition(0, ResourceToolkit.GetLocalizedString(StringNames.Video)));
        newSection.SetFirstPageData(videos, page);
        return newSection;
    }

    private void SetSearchError(Exception error)
        => SearchErrorMessage = ResourceToolkit.GetLocalizedString(error is SearchVerificationRequiredException
            ? StringNames.SearchVerificationRequired : StringNames.SearchFailed);

    private ISearchSectionDetailViewModel CreateSection(string keyword, SearchSectionType type)
    {
        var sectionDetail = type switch
        {
            SearchSectionType.Anime or SearchSectionType.Cinema => (ISearchSectionDetailViewModel)new PgcSearchSectionDetailViewModel(_service),
            SearchSectionType.Live => new LiveSearchSectionDetailViewModel(_service),
            SearchSectionType.User => new UserSearchSectionDetailViewModel(_service),
            SearchSectionType.Article => new ArticleSearchSectionDetailViewModel(_service),
            _ => default
        };

        var partition = type switch
        {
            SearchSectionType.Anime => new SearchPartition(7, ResourceToolkit.GetLocalizedString(StringNames.Anime)),
            SearchSectionType.Cinema => new SearchPartition(8, ResourceToolkit.GetLocalizedString(StringNames.Cinema)),
            SearchSectionType.Live => new SearchPartition(4, ResourceToolkit.GetLocalizedString(StringNames.Live)),
            SearchSectionType.User => new SearchPartition(2, ResourceToolkit.GetLocalizedString(StringNames.User)),
            SearchSectionType.Article => new SearchPartition(6, ResourceToolkit.GetLocalizedString(StringNames.Article)),
            _ => default
        };

        sectionDetail?.Initialize(keyword, partition);
        return sectionDetail;
    }
}
