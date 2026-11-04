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
        _box = new((ISearchService)_service, NullLogger<SearchBoxViewModel>.Instance);
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
        var events = _logger.Entries.Select(e => e.Message.Split(' ')).ToArray();
        Assert.All(events, e => Assert.True(Guid.TryParse(e[1], out _)));
        Assert.Single(events.Select(e => e[1]).Distinct());
        Assert.Equal(3, events.Where(e => e[3] == "accepted").Select(e => e[2]).Distinct().Count());
    }

    private sealed class CaptureLogger : ILogger<SearchPageViewModel>
    {
        public List<(string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((formatter(state, exception), exception));
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



