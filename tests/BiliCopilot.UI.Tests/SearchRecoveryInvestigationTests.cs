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

// Explicit investigation reproductions for #124. Red cases are handed to #125 to turn green.
public sealed class SearchRecoveryInvestigationTests : IDisposable
{
    private readonly NavigationViewModel _navigation = new();
    private readonly OverlayFrame _overlay = new();
    private readonly DispatcherQueue _dispatcher = new();
    private readonly ControlledService _service = DispatchProxy.Create<ISearchService, ControlledService>() as ControlledService ?? throw new InvalidOperationException();
    private readonly SearchPageViewModel _model;
    private readonly SearchBoxViewModel _box;

    public SearchRecoveryInvestigationTests()
    {
        _model = new((ISearchService)_service, NullLogger<SearchPageViewModel>.Instance);
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
    [Trait("Category", "InvestigationRed")]
    public async Task Investigation_CompletedEmptyAllowsExplicitSameKeywordRetry()
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
    [Trait("Category", "InvestigationRed")]
    public async Task Investigation_LateEmptyResponseMustNotOverwriteNewKeywordVideo()
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
    public async Task LateEmptyOverwriteDoesNotPermanentlyBlockAnotherDifferentKeyword()
    {
        Submit("query-a");
        var old = _model.SearchCommand.ExecutionTask!;
        Submit("query-b");
        await Complete(1);
        _service.Requests[0].Completion.SetResult(([], null));
        await old;
        _dispatcher.Drain();
        Assert.True(VideoSection.IsEmpty);
        Submit("query-c");
        Assert.Equal(3, _service.Requests.Count);
        await Complete(2);
        Assert.Equal("query-c", _model.Keyword);
        Assert.Single(VideoSection.Items);
    }
    public void Dispose() { NavigationTestServices.Current.Value = null; NavigationTestServices.Settings.Value = null; }
    private sealed class Settings : ISettingsToolkit { public T ReadLocalSetting<T>(string key, T fallback) => fallback; }
    public sealed record Request(string Keyword, CancellationToken Token)
    {
        public TaskCompletionSource<(IReadOnlyList<VideoInformation>, int?)> Completion { get; } = new();
    }
    public class ControlledService : DispatchProxy
    {
        public List<Request> Requests { get; } = [];
        public TaskCompletionSource<IReadOnlyList<SearchSuggestItem>> Suggestions { get; } = new();
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(ISearchService.GetComprehensiveSearchResultAsync))
            {
                var request = new Request((string)args![0]!, (CancellationToken)args[3]!);
                Requests.Add(request);
                return request.Completion.Task;
            }
            if (method.Name == nameof(ISearchService.GetSearchSuggestsAsync)) return Suggestions.Task;
            throw new NotSupportedException(method.Name);
        }
    }
}
#endif



