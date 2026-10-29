// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Search;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Search;

namespace BiliCopilot.UI.Tests;

public sealed class SearchSuggestionTests
{
    [Fact]
    public async Task BurstRequestsOnlyFinalKeywordAfter250MillisecondsOfQuiet()
    {
        var clock = new ManualClock();
        var service = new SearchService { CompleteImmediately = true };
        var model = new SearchBoxViewModel(service, new SearchLogger(), clock) { Keyword = "b" };
        var first = model.DebounceSuggestionsCommand.ExecuteAsync(null);
        clock.Advance(200);
        model.Keyword = "bi";
        var second = model.DebounceSuggestionsCommand.ExecuteAsync(null);
        clock.Advance(200);
        model.Keyword = "bili";
        var last = model.DebounceSuggestionsCommand.ExecuteAsync(null);
        clock.Advance(249);
        Assert.Empty(service.Requests);
        clock.Advance(1);
        await Task.WhenAll(first, second, last);
        Assert.Equal("bili", Assert.Single(service.Requests).Keyword);
        Assert.Equal("bili", Assert.Single(model.Suggestion).Keyword);
    }

    [Fact]
    public async Task NewInputRejectsLateResultsBeforeAnotherRequestStarts()
    {
        var service = new SearchService();
        var model = new SearchBoxViewModel(service, new SearchLogger()) { Keyword = "old" };
        var old = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        var request = Assert.Single(service.Requests);

        model.Keyword = "new";
        request.Completion.SetResult([new("Old result", "old")]);
        await old;

        Assert.True(request.Token.IsCancellationRequested);
        Assert.Empty(model.Suggestion);
    }

    [Fact]
    public async Task NewestRequestWinsEvenWhenKeywordIsUnchanged()
    {
        var service = new SearchService();
        var model = new SearchBoxViewModel(service, new SearchLogger()) { Keyword = "same" };
        var first = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        Assert.True(model.ReloadSuggestionsCommand.CanExecute(null));
        var last = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        Assert.True(service.Requests[0].Token.IsCancellationRequested);
        service.Requests[1].Completion.SetResult([new("Latest", "latest")]);
        await last;
        service.Requests[0].Completion.SetResult([new("Stale", "stale")]);
        await first;
        Assert.Equal("latest", Assert.Single(model.Suggestion).Keyword);
    }

    [Fact]
    public async Task OldCompletionCannotReleaseTheNewRequestsCancellation()
    {
        var service = new SearchService();
        var model = new SearchBoxViewModel(service, new SearchLogger()) { Keyword = "old" };
        var old = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        model.Keyword = "new";
        var current = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        service.Requests[0].Completion.SetResult([new("Old", "old")]);
        await old;
        model.TryCancelSuggestCommand.Execute(null);
        Assert.True(service.Requests[1].Token.IsCancellationRequested);
        service.Requests[1].Completion.SetResult([new("New", "new")]);
        await current;
        Assert.Empty(model.Suggestion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrClearDuringDebouncePreventsAnyRequest(bool clear)
    {
        var clock = new ManualClock();
        var logger = new SearchLogger();
        var service = new SearchService { CompleteImmediately = true };
        var model = new SearchBoxViewModel(service, logger, clock) { Keyword = "pending" };
        var pending = model.DebounceSuggestionsCommand.ExecuteAsync(null);
        Assert.True(model.DebounceSuggestionsCommand.CanExecute(null));
        clock.Advance(249);
        if (clear) model.Keyword = "";
        else model.TryCancelSuggestCommand.Execute(null);
        clock.Advance(1);
        await pending;
        Assert.Empty(service.Requests);
        Assert.Empty(service.Recommendations);
        Assert.Empty(model.Suggestion);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task NewInputImmediatelyClearsAlreadyVisibleSuggestions()
    {
        var service = new SearchService { CompleteImmediately = true };
        var model = new SearchBoxViewModel(service, new SearchLogger()) { Keyword = "old" };
        await model.ReloadSuggestionsCommand.ExecuteAsync(null);
        Assert.Equal("old", Assert.Single(model.Suggestion).Keyword);
        model.Keyword = "new";
        Assert.Empty(model.Suggestion);
        Assert.Single(service.Requests);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RealFailuresRemainDiagnosableEvenAfterCancellation(bool recommendations, bool cancel)
    {
        var service = new SearchService();
        var logger = new SearchLogger();
        var model = new SearchBoxViewModel(service, logger) { Keyword = recommendations ? "" : "query" };
        var loading = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        if (cancel) model.TryCancelSuggestCommand.Execute(null);
        var failure = new HttpRequestException("Service unavailable");
        if (recommendations) service.Recommendations[0].Completion.SetException(failure);
        else service.Requests[0].Completion.SetException(failure);
        await loading;
        Assert.Same(failure, Assert.Single(logger.Errors));
        Assert.Empty(model.Suggestion);
    }

    [Fact]
    public async Task UnrequestedCancellationRemainsDiagnosable()
    {
        var service = new SearchService();
        var logger = new SearchLogger();
        var model = new SearchBoxViewModel(service, logger) { Keyword = "query" };
        var loading = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        var timeout = new OperationCanceledException("Transport timeout");
        service.Requests[0].Completion.SetException(timeout);
        await loading;
        Assert.Same(timeout, Assert.Single(logger.Errors));
    }

    [Fact]
    public async Task EmptyFocusUsesRecommendationCacheAndHonorsDisabledSetting()
    {
        var service = new SearchService { CompleteImmediately = true };
        var model = new SearchBoxViewModel(service, new SearchLogger());
        var focus = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        Assert.Single(service.Recommendations).Completion.SetResult([new("1", 0, "recommended", "Recommendation")]);
        await focus;
        Assert.Equal("recommended", Assert.Single(model.Suggestion).Keyword);
        model.Keyword = "query";
        await model.ReloadSuggestionsCommand.ExecuteAsync(null);
        Assert.Equal("query", Assert.Single(model.Suggestion).Keyword);
        model.Keyword = "";
        await model.ReloadSuggestionsCommand.ExecuteAsync(null);
        Assert.Equal("recommended", Assert.Single(model.Suggestion).Keyword);
        Assert.Single(service.Recommendations);
        try
        {
            Toolkits.SettingsToolkit.ShowSearchRecommend = false;
            await model.ReloadSuggestionsCommand.ExecuteAsync(null);
            Assert.Empty(model.Suggestion);
            Assert.Single(service.Recommendations);
        }
        finally
        {
            Toolkits.SettingsToolkit.ShowSearchRecommend = true;
        }
    }

    [Fact]
    public async Task RegionSwitchRejectsGlobalResultsAndRestoresBothChoicesAfterSubmission()
    {
        var service = new SearchService();
        var model = new SearchBoxViewModel(service, new SearchLogger()) { Keyword = "video" };
        var global = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        model.SetExtraRegion("history", "History");
        Assert.True(service.Requests[0].Token.IsCancellationRequested);
        await model.ReloadSuggestionsCommand.ExecuteAsync(null);
        service.Requests[0].Completion.SetResult([new("Stale global", "stale")]);
        await global;
        Assert.Equal(new[] { "global", "history" }, model.Suggestion.Select(item => item.RegionId));
        Assert.All(model.Suggestion, item => Assert.Equal("video", item.Keyword));
        model.TryCancelSuggestCommand.Execute(null);
        model.Suggestion.Clear();
        await model.ReloadSuggestionsCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "global", "history" }, model.Suggestion.Select(item => item.RegionId));
        Assert.All(model.Suggestion, item => Assert.Equal("video", item.SearchContent));
        model.Keyword = "";
        await model.ReloadSuggestionsCommand.ExecuteAsync(null);
        Assert.Equal(2, model.Suggestion.Count);
        Assert.All(model.Suggestion, item => Assert.Equal("", item.Keyword));
        Assert.Single(service.Requests);
        Assert.Empty(service.Recommendations);
    }

    private sealed class SearchLogger : ILogger<SearchBoxViewModel>
    {
        public List<Exception> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception is not null) Errors.Add(exception);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalCancellationIsQuietIncludingTransportWrappedCancellation(bool wrapped)
    {
        var service = new SearchService();
        var logger = new SearchLogger();
        var model = new SearchBoxViewModel(service, logger) { Keyword = "old" };
        var loading = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        var request = Assert.Single(service.Requests);
        model.TryCancelSuggestCommand.Execute(null);
        Exception error = new OperationCanceledException(request.Token);
        if (wrapped) error = new Exception("Transport wrapper", error);
        request.Completion.SetException(error);
        await loading;
        Assert.Empty(logger.Errors);
        Assert.Empty(model.Suggestion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledRecommendationsCannotReopenSuggestionsOrPopulateCache(bool changeKeyword)
    {
        var service = new SearchService();
        var model = new SearchBoxViewModel(service, new SearchLogger());
        var loading = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        var request = Assert.Single(service.Recommendations);
        if (changeKeyword) model.Keyword = "new";
        else model.TryCancelSuggestCommand.Execute(null);
        request.Completion.SetResult([new("1", 0, "stale", "Stale recommendation")]);
        await loading;
        Assert.True(request.Token.IsCancellationRequested);
        Assert.Empty(model.Suggestion);

        model.Keyword = "";
        var reloaded = model.ReloadSuggestionsCommand.ExecuteAsync(null);
        Assert.Equal(2, service.Recommendations.Count);
        service.Recommendations[1].Completion.SetResult([new("2", 0, "fresh", "Fresh recommendation")]);
        await reloaded;
        Assert.Equal("fresh", Assert.Single(model.Suggestion).Keyword);
    }

    private sealed record RecommendationRequest(CancellationToken Token)
    {
        public TaskCompletionSource<IReadOnlyList<SearchRecommendItem>> Completion { get; } = new();
    }

    private sealed record Request(string Keyword, CancellationToken Token)
    {
        public TaskCompletionSource<IReadOnlyList<SearchSuggestItem>> Completion { get; } = new();
    }

    private sealed class SearchService : ISearchService
    {
        public bool CompleteImmediately { get; init; }
        public List<Request> Requests { get; } = [];
        public List<RecommendationRequest> Recommendations { get; } = [];
        public Task<IReadOnlyList<SearchSuggestItem>> GetSearchSuggestsAsync(string keyword, CancellationToken cancellationToken = default)
        {
            var request = new Request(keyword, cancellationToken);
            Requests.Add(request);
            if (CompleteImmediately) request.Completion.SetResult([new(keyword, keyword)]);
            return request.Completion.Task;
        }

        public Task<IReadOnlyList<SearchRecommendItem>> GetSearchRecommendsAsync(CancellationToken cancellationToken = default)
        {
            var request = new RecommendationRequest(cancellationToken);
            Recommendations.Add(request);
            return request.Completion.Task;
        }
        public Task<IReadOnlyList<HotSearchItem>> GetTotalHotSearchAsync(int count = 30, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation> Videos, int? NextPage)> GetComprehensiveSearchResultAsync(string keyword, int? page = default, ComprehensiveSearchSortType sort = ComprehensiveSearchSortType.Default, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<SearchResultItem> Result, string? Offset)> GetPartitionSearchResultAsync(string keyword, SearchPartition partition, string? offset = default, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation>? Videos, int TotalCount, bool HasMore)> SearchUserVideosAsync(string userId, string keyword, int pageNumber = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<VideoInformation>? Videos, int TotalCount, bool HasMore)> SearchHistoryVideosAsync(string keyword, int pageNumber = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // A local timer for this search behavior test; no sleeps, new packages, or production internals.
    private sealed class ManualClock : TimeProvider
    {
        private long _milliseconds;
        private readonly List<Timer> _timers = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(this, callback, state);
            _timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(int milliseconds)
        {
            _milliseconds += milliseconds;
            foreach (var timer in _timers.ToArray()) timer.FireIfDue();
        }

        private sealed class Timer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long _due = long.MaxValue;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Assert.Equal(Timeout.InfiniteTimeSpan, period);
                _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._milliseconds + (long)dueTime.TotalMilliseconds;
                return true;
            }

            public void FireIfDue()
            {
                if (_due > clock._milliseconds) return;
                _due = long.MaxValue;
                callback(state);
            }

            public void Dispose() => _due = long.MaxValue;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
