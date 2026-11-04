#if SEARCH_RECOVERY_INVESTIGATION
using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Search;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Search;
using Richasy.WinUIKernel.Share.Toolkits;
using System.Reflection;

namespace BiliCopilot.UI.Tests;

// Regression for #125: production submission, navigation, page and video/PGC commands.
public sealed class SearchRecoveryInvestigationTests : IDisposable
{
    private readonly NavigationViewModel _navigation = new();
    private readonly OverlayFrame _overlay = new();
    private readonly DispatcherQueue _dispatcher = new();
    private readonly ControlledService _service = DispatchProxy.Create<ISearchService, ControlledService>() as ControlledService ?? throw new InvalidOperationException();
    private readonly CaptureLogger _logger = new();
    private readonly SearchPageViewModel _model;
    private readonly SearchBoxViewModel _box;

    public SearchRecoveryInvestigationTests()
    {
        _model = new((ISearchService)_service, _logger);
        _box = new((ISearchService)_service, _logger);
        var app = new AppViewModel();
        app.Windows.Add(new MainWindow());
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new()
        {
            [typeof(AppViewModel)] = app,
            [typeof(NavigationViewModel)] = _navigation,
            [typeof(SearchBoxViewModel)] = _box,
            [typeof(SearchPageViewModel)] = _model,
            [typeof(DispatcherQueue)] = _dispatcher,
            [typeof(ILogger<VideoSearchSectionDetailViewModel>)] = NullLogger<VideoSearchSectionDetailViewModel>.Instance,
            [typeof(ILogger<PgcSearchSectionDetailViewModel>)] = NullLogger<PgcSearchSectionDetailViewModel>.Instance,
            [typeof(ISettingsToolkit)] = new Settings(),
        };
        _navigation.Initialize(new(), new MainFrame(), _overlay);
    }

    private SearchPage Submit(string keyword)
    {
        _box.SearchCommand.Execute(new SearchSuggestItemViewModel(new SearchSuggestItem(keyword, keyword)));
        var page = Assert.IsType<SearchPage>(_overlay.GetCurrentContent());
        if (!page.IsLoaded) page.Load(); // Explicit OS Loaded event; reused pages do not get another Loaded.
        return page;
    }

    private async Task Complete(int index, bool empty = false, bool fail = false)
    {
        var command = _model.SearchCommand.ExecutionTask!;
        if (fail) _service.Requests[index].Completion.SetException(new IOException("controlled offline"));
        else _service.Requests[index].Completion.SetResult((empty ? [] : [new VideoInformation(new MediaIdentifier("42", null, null), default!)], null));
        await command;
        _dispatcher.Drain();
    }

    private VideoSearchSectionDetailViewModel VideoSection => Assert.IsType<VideoSearchSectionDetailViewModel>(_model.SelectedSection);

    [Fact]
    public async Task PendingOldRetryDoesNotDisableLatestErrorRetry()
    {
        Submit("query-a");
        _service.Requests[0].Completion.SetException(new SearchVerificationRequiredException());
        await _model.SearchCommand.ExecutionTask!;
        var oldRetry = _model.RetryCommand.ExecuteAsync(null);
        Submit("query-b");
        await Complete(2, fail: true);
        Assert.True(_model.RetryCommand.CanExecute(null));
        var retry = _model.RetryCommand.ExecuteAsync(null);
        Assert.Equal("query-b", _service.Requests[3].Keyword);
        _service.Requests[3].Completion.SetResult(([new VideoInformation(new MediaIdentifier("42", null, null), default!)], null));
        await retry;
        _dispatcher.Drain();
        _service.Requests[1].Completion.SetException(new SearchVerificationRequiredException());
        await oldRetry;
        Assert.False(_model.HasSearchError);
        Assert.Single(VideoSection.Items);
        Assert.False(_model.IsSearching);
    }

    [Fact]
    public async Task UnloadClearsPublishedVerificationAndDisablesRetry()
    {
        var page = Submit("query-a");
        _service.Requests[0].Completion.SetException(new SearchVerificationRequiredException());
        await _model.SearchCommand.ExecutionTask!;
        Assert.True(_model.RetryCommand.CanExecute(null));
        page.Unload();
        Assert.Null(_model.SearchErrorMessage);
        Assert.False(_model.HasSearchError);
        Assert.False(_model.RetryCommand.CanExecute(null));
        await _model.RetryCommand.ExecuteAsync(null);
        Assert.Single(_service.Requests);
    }

    [Fact]
    public async Task OrdinaryFailureHasDistinctMessageAndNormalEmptyHasNoError()
    {
        Submit("query-a");
        await Complete(0, fail: true);
        Assert.True(_model.HasSearchError);
        Assert.Equal("SearchFailed", _model.SearchErrorMessage);
        Assert.True(_model.RetryCommand.CanExecute(null));
        Submit("query-b");
        Assert.False(_model.HasSearchError);
        Assert.False(_model.RetryCommand.CanExecute(null));
        await Complete(1, empty: true);
        Assert.False(_model.HasSearchError);
        Assert.Null(_model.SearchErrorMessage);
        Assert.True(VideoSection.IsEmpty);
        Assert.False(_model.RetryCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task LateVerificationCannotReplaceLatestPendingErrorOrResults(int latest)
    {
        Submit("query-a");
        var old = _model.SearchCommand.ExecutionTask!;
        Submit("query-b");
        if (latest == 1) await Complete(1, fail: true);
        if (latest == 2) await Complete(1);
        _service.Requests[0].Completion.SetException(new SearchVerificationRequiredException());
        await old;
        _dispatcher.Drain();
        Assert.Equal("query-b", _model.Keyword);
        Assert.Equal(latest == 1 ? "SearchFailed" : null, _model.SearchErrorMessage);
        if (latest == 0)
        {
            Assert.True(_model.IsSearching);
            await Complete(1);
        }
        if (latest != 1) Assert.Single(VideoSection.Items);
    }

    [Fact]
    public async Task NewQueryClearsVerificationAndUnloadDiscardsLateVerification()
    {
        var page = Submit("query-a");
        _service.Requests[0].Completion.SetException(new SearchVerificationRequiredException());
        await _model.SearchCommand.ExecutionTask!;
        Assert.True(_model.HasSearchError);
        Submit("query-b");
        Assert.False(_model.HasSearchError);
        var pending = _model.SearchCommand.ExecutionTask!;
        page.Unload();
        _service.Requests[1].Completion.SetException(new SearchVerificationRequiredException());
        await pending;
        Assert.False(_model.HasSearchError);
        Assert.False(_model.IsSearching);
        _navigation.Back();
        Submit("query-b");
        await Complete(2);
        Assert.Single(VideoSection.Items);
        Assert.False(_model.HasSearchError);
    }

    [Fact]
    public async Task VerificationDiagnosticsNeverExposeExceptionPayload()
    {
        var marker = Guid.NewGuid().ToString();
        Submit(marker);
        var exception = new SearchVerificationRequiredException();
        exception.Data["v_voucher"] = marker;
        _service.Requests[0].Completion.SetException(exception);
        await _model.SearchCommand.ExecutionTask!;
        Assert.Contains(_logger.Entries, e => e.Message.Contains("verification True"));
        Assert.All(_logger.Entries, e =>
        {
            Assert.Null(e.Exception);
            Assert.DoesNotContain(marker, e.Message);
            Assert.DoesNotContain("v_voucher", e.Message);
            Assert.DoesNotContain(exception.Message, e.Message);
        });
        Assert.All(_logger.Values, value =>
        {
            Assert.False(value is Exception);
            Assert.DoesNotContain(marker, value?.ToString() ?? "");
            Assert.DoesNotContain("v_voucher", value?.ToString() ?? "");
        });
        Assert.Equal("SearchVerificationRequired", _model.SearchErrorMessage);
    }

    [Fact]
    public async Task VerificationRefreshWithoutSectionRetriesSameQueryAndRecovers()
    {
        Submit("query-a");
        _service.Requests[0].Completion.SetException(new SearchVerificationRequiredException());
        await _model.SearchCommand.ExecutionTask!;
        var retry = _model.RetryCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "query-a", "query-a" }, _service.Requests.Select(r => r.Keyword));
        Assert.False(_model.HasSearchError);
        _service.Requests[1].Completion.SetResult(([new VideoInformation(new MediaIdentifier("42", null, null), default!)], null));
        await retry;
        _dispatcher.Drain();
        Assert.Single(VideoSection.Items);
        Assert.False(_model.HasSearchError);
    }

    [Fact]
    public async Task VerificationFailurePublishesPersistentVisibleErrorState()
    {
        Submit("private-query");
        var changes = new List<string?>();
        _model.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        _service.Requests[0].Completion.SetException(new SearchVerificationRequiredException());
        await _model.SearchCommand.ExecutionTask!;
        _dispatcher.Drain();
        Assert.True(_model.HasSearchError);
        Assert.Equal("SearchVerificationRequired", _model.SearchErrorMessage);
        Assert.Contains(nameof(_model.HasSearchError), changes);
        Assert.Contains(nameof(_model.SearchErrorMessage), changes);
        Assert.False(_model.IsSearching);
        Assert.Null(_model.SelectedSection);
        Assert.True(_model.RetryCommand.CanExecute(null));
        Assert.Single(_service.Requests);
        _dispatcher.Drain();
        Assert.True(_model.HasSearchError);
    }

    [Fact]
    public async Task CompletedEmptyThenDifferentKeywordPublishesNewVideoOnReusedPage()
    {
        var page = Submit("query-a");
        await Complete(0, empty: true);
        Assert.True(VideoSection.IsEmpty);
        Assert.Same(page, Submit("query-b"));
        Assert.Equal(new[] { "query-a", "query-b" }, _service.Requests.Select(r => r.Keyword));
        await Complete(1);
        Assert.Equal("query-b", _model.Keyword);
        Assert.Equal("42", Assert.Single(VideoSection.Items).Data.Identifier.Id);
        Assert.False(VideoSection.IsEmpty);
    }

    [Fact]
    public async Task CompletedEmptyAllowsExplicitSameKeywordRetry()
    {
        Submit("query-a");
        await Complete(0, empty: true);
        Submit("query-a");
        Assert.Equal(2, _service.Requests.Count);
        await Complete(1);
        Assert.Single(VideoSection.Items);
    }

    [Fact]
    public async Task FailedThenDifferentKeywordRecovers()
    {
        Submit("query-a");
        await Complete(0, fail: true);
        Assert.Null(_model.Sections);
        Assert.False(_model.IsSearching);
        Submit("query-b");
        await Complete(1);
        Assert.Equal("query-b", _model.Keyword);
        Assert.Single(VideoSection.Items);
    }

    [Fact]
    public async Task FailedThenSameKeywordRetries()
    {
        Submit("query-a");
        await Complete(0, fail: true);
        Submit("query-a");
        Assert.Equal(2, _service.Requests.Count);
        await Complete(1);
        Assert.Single(VideoSection.Items);
    }

    [Fact]
    public async Task LateEmptyResponseMustNotOverwriteNewKeywordVideo()
    {
        Submit("query-a");
        var old = _model.SearchCommand.ExecutionTask!;
        Assert.False(_model.SearchCommand.CanExecute("query-b"));
        Submit("query-b"); // Real call site invokes Execute directly, bypassing CanExecute.
        Assert.Equal(new[] { "query-a", "query-b" }, _service.Requests.Select(r => r.Keyword));
        await Complete(1);
        Assert.Single(VideoSection.Items);
        _service.Requests[0].Completion.SetResult(([], null));
        await old;
        _dispatcher.Drain();
        Assert.Equal("query-b", _model.Keyword);
        Assert.Single(VideoSection.Items);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OldCompletionDoesNotEndLatestLoading(bool fail, bool cancel)
    {
        Submit("query-a");
        var old = _model.SearchCommand.ExecutionTask!;
        Submit("query-b");
        if (fail) _service.Requests[0].Completion.SetException(new IOException("secret query-a credential"));
        else if (cancel) _service.Requests[0].Completion.SetCanceled();
        else _service.Requests[0].Completion.SetResult(([], null));
        await old;
        _dispatcher.Drain();
        Assert.True(_model.IsSearching);
        Assert.Null(_model.Sections);
        Assert.Equal("query-b", _model.Keyword);
        await Complete(1);
        Assert.False(_model.IsSearching);
        Assert.Single(VideoSection.Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OldErrorDoesNotChangePublishedLatestResults(bool cancel)
    {
        Submit("query-a");
        var old = _model.SearchCommand.ExecutionTask!;
        Submit("query-b");
        await Complete(1);
        var section = VideoSection;
        if (cancel) _service.Requests[0].Completion.SetCanceled();
        else _service.Requests[0].Completion.SetException(new IOException("secret"));
        await old;
        Assert.Same(section, VideoSection);
        Assert.Single(section.Items);
        Assert.False(_model.IsSearching);
    }

    [Fact]
    public async Task UnloadedPageDiscardsPendingResponse()
    {
        var page = Submit("query-a");
        var pending = _model.SearchCommand.ExecutionTask!;
        page.Unload();
        _service.Requests[0].Completion.SetResult(([], null));
        await pending;
        _dispatcher.Drain();
        Assert.Null(_model.Sections);
        Assert.False(_model.IsSearching);
        _navigation.Back();
        Submit("query-a");
        await Complete(1);
        Assert.Single(VideoSection.Items);
    }

    [Fact]
    public async Task LatestCancellationAllowsSameKeywordRetry()
    {
        Submit("query-a");
        var pending = _model.SearchCommand.ExecutionTask!;
        _service.Requests[0].Completion.SetCanceled();
        await pending;
        Assert.False(_model.IsSearching);
        Submit("query-a");
        await Complete(1);
        Assert.Single(VideoSection.Items);
    }

    [Fact]
    public async Task EmptySectionRefreshCommandCanRecoverWhenInvokedDirectly()
    {
        Submit("query-a");
        await Complete(0, empty: true);
        var section = VideoSection;
        await section.RefreshCommand.ExecuteAsync(null);
        _dispatcher.Drain();
        Assert.Equal(2, _service.Requests.Count);
        _service.Requests[1].Completion.SetResult(([new VideoInformation(new MediaIdentifier("42", null, null), default!)], null));
        // The production refresh queues async void; drain its captured dispatcher continuation.
        _dispatcher.Drain();
        Assert.Single(section.Items);
        Assert.False(section.IsEmpty);
    }

    [Fact]
    public async Task EmptySectionRefreshVerificationPublishesPageErrorAndAllowsRetry()
    {
        Submit("query-a");
        await Complete(0, empty: true);
        await VideoSection.RefreshCommand.ExecuteAsync(null);
        _dispatcher.Drain();
        Assert.Equal(2, _service.Requests.Count);
        _service.Requests[1].Completion.SetException(new SearchVerificationRequiredException());
        _dispatcher.Drain();
        Assert.True(_model.HasSearchError);
        Assert.Equal("SearchVerificationRequired", _model.SearchErrorMessage);
        Assert.True(_model.RetryCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulSectionRefreshOrSortClearsPreviousVerification(bool sort)
    {
        Submit("query-a");
        await Complete(0, empty: true);
        var section = VideoSection;
        await section.RefreshCommand.ExecuteAsync(null);
        _dispatcher.Drain();
        _service.Requests[1].Completion.SetException(new SearchVerificationRequiredException());
        _dispatcher.Drain();
        Assert.True(_model.HasSearchError);

        if (sort) section.ChangeSortTypeCommand.Execute(ComprehensiveSearchSortType.Newest);
        else await section.RefreshCommand.ExecuteAsync(null);
        _dispatcher.Drain();
        _service.Requests[2].Completion.SetResult(([new VideoInformation(new MediaIdentifier("42", null, null), default!)], null));
        _dispatcher.Drain();

        Assert.Single(section.Items);
        Assert.False(_model.HasSearchError);
        Assert.Null(_model.SearchErrorMessage);
        Assert.False(_model.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task OldSectionSuccessDoesNotClearLatestQueryVerification()
    {
        Submit("query-a");
        await Complete(0, empty: true);
        await VideoSection.RefreshCommand.ExecuteAsync(null);
        _dispatcher.Drain();
        Submit("query-b");
        var latest = _model.SearchCommand.ExecutionTask!;
        _service.Requests[2].Completion.SetException(new SearchVerificationRequiredException());
        await latest;
        Assert.True(_model.HasSearchError);

        _service.Requests[1].Completion.SetResult(([new VideoInformation(new MediaIdentifier("42", null, null), default!)], null));
        _dispatcher.Drain();
        Assert.True(_model.HasSearchError);
        Assert.Equal("SearchVerificationRequired", _model.SearchErrorMessage);
        Assert.Equal("query-b", _model.Keyword);
    }

    [Fact]
    public async Task QueuedFirstPagePublicationDoesNotClearPaginationVerification()
    {
        Submit("query-a");
        var firstLoad = _model.SearchCommand.ExecutionTask!;
        _service.Requests[0].Completion.SetResult(([new VideoInformation(new MediaIdentifier("42", null, null), default!)], 2));
        await firstLoad;
        var paging = VideoSection.LoadItemsCommand.ExecuteAsync(null);
        _service.Requests[1].Completion.SetException(new SearchVerificationRequiredException());
        await paging;
        Assert.True(_model.HasSearchError);

        _dispatcher.Drain();
        Assert.True(_model.HasSearchError);
        Assert.Equal("SearchVerificationRequired", _model.SearchErrorMessage);
    }

    [Fact]
    public async Task OldSectionRefreshVerificationCannotReplaceNewQueryResults()
    {
        Submit("query-a");
        await Complete(0, empty: true);
        await VideoSection.RefreshCommand.ExecuteAsync(null);
        _dispatcher.Drain();
        Submit("query-b");
        await Complete(2);
        _service.Requests[1].Completion.SetException(new SearchVerificationRequiredException());
        _dispatcher.Drain();
        Assert.False(_model.HasSearchError);
        Assert.Single(VideoSection.Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaginationOrSortVerificationPublishesPageError(bool sort)
    {
        Submit("query-a");
        var firstLoad = _model.SearchCommand.ExecutionTask!;
        _service.Requests[0].Completion.SetResult(([new VideoInformation(new MediaIdentifier("42", null, null), default!)], 2));
        await firstLoad;
        _dispatcher.Drain();
        var section = VideoSection;
        Task loading = Task.CompletedTask;
        if (sort)
        {
            section.ChangeSortTypeCommand.Execute(ComprehensiveSearchSortType.Newest);
            _dispatcher.Drain();
        }
        else
        {
            loading = section.LoadItemsCommand.ExecuteAsync(null);
        }

        Assert.Equal(2, _service.Requests.Count);
        _service.Requests[1].Completion.SetException(new SearchVerificationRequiredException());
        await loading;
        _dispatcher.Drain();
        Assert.True(_model.HasSearchError);
        Assert.Equal("SearchVerificationRequired", _model.SearchErrorMessage);
    }

    [Fact]
    public async Task CloseAndReopenSearchConstructsPageAndDifferentKeywordRecovers()
    {
        var original = Submit("query-a");
        await Complete(0, empty: true);
        original.Unload();
        _navigation.Back();
        var reopened = Submit("query-b");
        Assert.NotSame(original, reopened);
        await Complete(1);
        Assert.Single(VideoSection.Items);
    }

    [Fact]
    public async Task SuggestionCancellationDoesNotCancelFormalSearch()
    {
        _box.Keyword = "query-a";
        var suggestions = _box.ReloadSuggestionsCommand.ExecuteAsync(null);
        Submit("query-b");
        _box.TryCancelSuggestCommand.Execute(null);
        _service.Suggestions.SetCanceled();
        await suggestions;
        Assert.False(_service.Requests[0].Token.IsCancellationRequested);
        await Complete(0);
        Assert.Single(VideoSection.Items);
    }

    [Fact]
    public async Task LateEmptyResponseStillAllowsAnotherDifferentKeyword()
    {
        Submit("query-a");
        var old = _model.SearchCommand.ExecutionTask!;
        Submit("query-b");
        await Complete(1);
        _service.Requests[0].Completion.SetResult(([], null));
        await old;
        _dispatcher.Drain();
        Assert.Single(VideoSection.Items);
        Submit("query-c");
        Assert.Equal(3, _service.Requests.Count);
        await Complete(2);
        Assert.Equal("query-c", _model.Keyword);
        Assert.Single(VideoSection.Items);
    }
    [Fact]
    public async Task LazyPartitionAndVideoRefreshKeepOriginalQuerySnapshot()
    {
        Submit("query-a");
        await Complete(0);
        var video = VideoSection;
        var anime = Assert.IsType<PgcSearchSectionDetailViewModel>(_model.Sections![1]);
        Submit("query-b");
        _model.SelectSectionCommand.Execute(anime);
        await anime.TryFirstLoadCommand.ExecutionTask!;
        Assert.Equal("query-a", Assert.Single(_service.PartitionKeywords));
        await video.RefreshCommand.ExecuteAsync(null);
        _dispatcher.Drain();
        Assert.Equal("query-a", _service.Requests[2].Keyword);
        _service.Requests[2].Completion.SetResult(([], null));
        _dispatcher.Drain();
        await Complete(1);
        Assert.Equal("query-b", _model.Keyword);
        Assert.Single(VideoSection.Items);
    }

    [Fact]
    public void RefreshBindingDependsOnSectionAvailabilityRatherThanEmptyResults()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(directory.FullName, "src/Desktop/BiliCopilot.UI/Controls/Search/SearchHeader.xaml"));
        var button = Assert.Single(document.Descendants(), e => e.Name.LocalName == "Button");
        Assert.Equal("{x:Bind ViewModel.SelectedSection, Mode=OneWay, Converter={StaticResource ObjectToBoolConverter}}", button.Attribute("IsEnabled")?.Value);
    }

    [Fact]
    public async Task LateSuccessCannotReplaceLatestEmptyResults()
    {
        Submit("query-a");
        var old = _model.SearchCommand.ExecutionTask!;
        Submit("query-b");
        await Complete(1, empty: true);
        var section = VideoSection;
        _service.Requests[0].Completion.SetResult(([new VideoInformation(new MediaIdentifier("99", null, null), default!)], null));
        await old;
        _dispatcher.Drain();
        Assert.Same(section, VideoSection);
        Assert.Empty(section.Items);
        Assert.True(section.IsEmpty);
    }

    [Fact]
    public async Task PaginationKeepsItsQueryAndOffsetWhileNewQueryIsPending()
    {
        Submit("query-a");
        var pending = _model.SearchCommand.ExecutionTask!;
        _service.Requests[0].Completion.SetResult(([new VideoInformation(new MediaIdentifier("42", null, null), default!)], 2));
        await pending;
        _dispatcher.Drain();
        var section = VideoSection;
        Submit("query-b");
        var more = section.LoadItemsCommand.ExecuteAsync(null);
        Assert.Equal("query-a", _service.Requests[2].Keyword);
        Assert.Equal(2, _service.Requests[2].Page);
        _service.Requests[2].Completion.SetResult(([], null));
        await more;
        await Complete(1);
        Assert.Equal("query-b", _model.Keyword);
        Assert.Single(VideoSection.Items);
    }

    [Fact]
    public async Task DiagnosticsCorrelateOutcomesWithoutKeywordOrExceptionPayload()
    {
        Submit("secret-keyword");
        await Complete(0, fail: true);
        Assert.Contains(_logger.Entries, e => e.Message.Contains("failure True"));
        Submit("secret-keyword");
        var old = _model.SearchCommand.ExecutionTask!;
        Submit("other-secret");
        await Complete(2, empty: true);
        _service.Requests[1].Completion.SetResult(([], null));
        await old;
        Assert.Contains(_logger.Entries, e => e.Message.Contains(" discard"));
        Assert.Contains(_logger.Entries, e => e.Message.Contains(" publish"));
        Assert.Contains(_logger.Entries, e => e.Message.Contains(" complete 0 "));
        Assert.All(_logger.Entries, e =>
        {
            Assert.Null(e.Exception);
            Assert.DoesNotContain("secret", e.Message);
            Assert.DoesNotContain("controlled offline", e.Message);
        });
        var events = _logger.Entries.Where(e => e.Message.StartsWith("Search ")).Select(e => e.Message.Split(' ')).ToArray();
        Assert.All(events, e => Assert.True(Guid.TryParse(e[1], out _)));
        Assert.Single(events.Select(e => e[1]).Distinct());
        Assert.Equal(3, events.Where(e => e[3] == "accepted").Select(e => e[2]).Distinct().Count());
    }

    [Fact]
    public async Task SubmissionBeforePageLoadIsVisibleAndCorrelatesWithStartedRequest()
    {
        _box.SearchCommand.Execute(new SearchSuggestItemViewModel(new SearchSuggestItem("secret-token", "secret-token")));
        var page = Assert.IsType<SearchPage>(_overlay.GetCurrentContent());
        Assert.Empty(_service.Requests);
        var submitted = Assert.Single(_logger.Entries, e => e.Message.EndsWith(" submitted"));
        var id = submitted.Message.Split(' ')[1];
        Assert.True(Guid.TryParse(id, out _));
        Assert.Contains(_logger.Entries, e => e.Message == $"SearchSubmission {id} navigation");
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains(" start"));
        page.Load();
        await Complete(0);
        Assert.Contains(_logger.Entries, e => e.Message.Contains($" submission {id}"));
        Assert.All(_logger.Entries, e => { Assert.Null(e.Exception); Assert.DoesNotContain("secret", e.Message); });
    }

    [Fact]
    public async Task ReusedPageSubmissionsHaveDistinctIdsAndRedactedFailure()
    {
        Submit("secret-first");
        await Complete(0, empty: true);
        Submit("secret-second");
        _service.Requests[1].Completion.SetException(new HttpRequestException("Cookie=secret; token=secret; secret-second"));
        await _model.SearchCommand.ExecutionTask!;
        var ids = _logger.Entries.Where(e => e.Message.EndsWith(" submitted")).Select(e => e.Message.Split(' ')[1]).ToArray();
        Assert.Equal(2, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Contains(_logger.Entries, e => e.Message.Contains($" submission {id}")));
        Assert.Contains(_logger.Entries, e => e.Message.Contains("network True"));
        Assert.All(_logger.Values, value =>
        {
            Assert.False(value is Exception);
            Assert.DoesNotContain("secret", value?.ToString() ?? "");
            Assert.DoesNotContain("Cookie", value?.ToString() ?? "");
        });
        Assert.All(_logger.Entries, e =>
        {
            Assert.Null(e.Exception);
            Assert.DoesNotContain("secret", e.Message);
            Assert.DoesNotContain("Cookie", e.Message);
            Assert.DoesNotContain("token", e.Message);
        });
    }

    [Fact]
    public async Task InputBoundaryRecordsSubmissionBeforeDispatch()
    {
        var id = _box.BeginSubmission(); // Same entry called first by QuerySubmitted.
        Assert.Equal($"SearchSubmission {id} submitted", Assert.Single(_logger.Entries).Message);
        Assert.Empty(_service.Requests);
        Assert.Null(_overlay.GetCurrentContent());
        _box.SubmitSearch(new SearchSuggestItemViewModel(new SearchSuggestItem("secret", "secret")), id);
        Assert.Contains(_logger.Entries, e => e.Message == $"SearchSubmission {id} command");
        Assert.IsType<SearchPage>(_overlay.GetCurrentContent()).Load();
        await Complete(0);
        Assert.Contains(_logger.Entries, e => e.Message == $"SearchSubmission {id} page");
        Assert.Contains(_logger.Entries, e => e.Message.Contains($" submission {id}"));
    }

    private sealed class CaptureLogger : ILogger<SearchPageViewModel>, ILogger<SearchBoxViewModel>
    {
        public List<(string Message, Exception? Exception)> Entries { get; } = [];
        public List<object?> Values { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((formatter(state, exception), exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                Values.AddRange(fields.Select(field => field.Value));
        }
    }

    public void Dispose() { NavigationTestServices.Current.Value = null; NavigationTestServices.Settings.Value = null; }
    private sealed class Settings : ISettingsToolkit { public T ReadLocalSetting<T>(string key, T fallback) => fallback; }
    public sealed record Request(string Keyword, CancellationToken Token, int? Page)
    {
        public TaskCompletionSource<(IReadOnlyList<VideoInformation>, int?)> Completion { get; } = new();
    }
    public class ControlledService : DispatchProxy
    {
        public List<string> PartitionKeywords { get; } = [];
        public List<Request> Requests { get; } = [];
        public TaskCompletionSource<IReadOnlyList<SearchSuggestItem>> Suggestions { get; } = new();
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(ISearchService.GetComprehensiveSearchResultAsync))
            {
                var request = new Request((string)args![0]!, (CancellationToken)args[3]!, (int?)args[1]);
                Requests.Add(request);
                return request.Completion.Task;
            }
            if (method.Name == nameof(ISearchService.GetPartitionSearchResultAsync))
            {
                PartitionKeywords.Add((string)args![0]!);
                return Task.FromResult<(IReadOnlyList<SearchResultItem>, string?)>(([], null));
            }
            if (method.Name == nameof(ISearchService.GetSearchSuggestsAsync)) return Suggestions.Task;
            throw new NotSupportedException(method.Name);
        }
    }
}
#endif



