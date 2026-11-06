// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Article;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Article;

namespace BiliCopilot.UI.Tests;

public sealed class ArticlePaginationTests : IDisposable
{
    private readonly DispatcherQueue _dispatcher = new();
    private readonly Errors _errors = new();

    public ArticlePaginationTests()
    {
        NavigationTestServices.Settings.Value = new();
        NavigationTestServices.Current.Value = new()
        {
            [typeof(ILogger<ArticlePartitionDetailViewModel>)] = _errors,
            [typeof(DispatcherQueue)] = _dispatcher,
        };
    }

    public void Dispose()
    {
        NavigationTestServices.Settings.Value = null;
        NavigationTestServices.Current.Value = null;
    }

    private ArticlePartitionDetailViewModel Create(string kind, Discovery service)
    {
        var root = new PartitionViewModel(new Partition(kind is "recommend" ? "_" : kind is "hot" ? "-" : "1", "Root"))
        {
            Children = [new(new Partition("2", "Child"))],
        };
        var vm = new ArticlePartitionDetailViewModel(root, service);
        vm.InitializeCommand.Execute(null);
        _dispatcher.Drain();
        if (kind == "child")
        {
            vm.ChangeChildPartitionCommand.Execute(root.Children[0]);
            _dispatcher.Drain();
        }

        return vm;
    }

    [Theory]
    [InlineData("recommend")]
    [InlineData("main")]
    [InlineData("child")]
    public async Task FailureStopsUpdateFeedbackAndRefreshRecovers(string kind)
    {
        var service = new Discovery();
        var vm = Create(kind, service);
        // Simulate the UI checking again after every list update, including a failure.
        var updates = 0;
        EventHandler feedback = (_, _) =>
        {
            if (++updates <= 4)
            {
                _dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => vm.LoadArticlesCommand.Execute(null));
            }
        };
        vm.ArticleListUpdated += feedback;
        service.Page = (_, _) => throw new InvalidOperationException("controlled article failure");
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        _dispatcher.Drain();
        var calls = service.Calls.Count;
        for (var i = 0; i < 4; i++)
        {
            await vm.LoadArticlesCommand.ExecuteAsync(null);
        }

        Assert.Equal(calls, service.Calls.Count);
        Assert.Single(_errors.Exceptions);
        Assert.IsType<InvalidOperationException>(_errors.Exceptions[0]);
        Assert.False(vm.IsArticleLoading);
        vm.ArticleListUpdated -= feedback;
        service.Page = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article("recovered")], page + 1));
        vm.RefreshCommand.Execute(null);
        _dispatcher.Drain();
        Assert.Equal(calls + 1, service.Calls.Count);
        Assert.Equal(0, service.Calls[^1].Page);
        Assert.Equal("recovered", Assert.Single(vm.Articles).Data.Identifier.Id);
        Assert.False(vm.IsArticleLoading);
    }

    [Theory]
    [InlineData("recommend", "empty")]
    [InlineData("main", "empty")]
    [InlineData("child", "empty")]
    [InlineData("recommend", "cursor")]
    [InlineData("main", "cursor")]
    [InlineData("child", "cursor")]
    [InlineData("recommend", "duplicates")]
    [InlineData("main", "duplicates")]
    [InlineData("child", "duplicates")]
    public async Task EndOrNoProgressBoundsFurtherRequests(string kind, string ending)
    {
        var service = new Discovery();
        var vm = Create(kind, service);
        service.Page = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>((ending == "empty" ? [] : [Article("initial")], ending == "cursor" ? page : page + 1));
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        // One overlapping page may have an advancing cursor; the second must stop.
        if (ending == "duplicates")
        {
            await vm.LoadArticlesCommand.ExecuteAsync(null);
        }

        var count = service.Calls.Count;
        for (var i = 0; i < 4; i++)
        {
            await vm.LoadArticlesCommand.ExecuteAsync(null);
        }

        Assert.Equal(count, service.Calls.Count);
        Assert.Single(vm.Articles);
        Assert.False(vm.IsArticleLoading);
    }

    [Theory]
    [InlineData("recommend")]
    [InlineData("main")]
    [InlineData("child")]
    public async Task AdvancingOverlapAllowsNextPageAndNewArticlesResetProgress(string kind)
    {
        var service = new Discovery();
        var vm = Create(kind, service);
        var start = service.Calls[^1].Page + 1;
        service.Page = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article(page == start || page == start + 2 ? "initial" : "new")], page + 1));
        for (var i = 0; i < 4; i++)
        {
            await vm.LoadArticlesCommand.ExecuteAsync(null);
        }

        Assert.Equal(new[] { start, start + 1, start + 2, start + 3 }, service.Calls.TakeLast(4).Select(c => c.Page));
        Assert.Equal(new[] { "initial", "new" }, vm.Articles.Select(a => a.Data.Identifier.Id));
    }

    [Fact]
    public async Task PartitionCachesAndStopsBelongToOutgoingListAndSortRestartsChild()
    {
        var service = new Discovery();
        var vm = Create("main", service);
        service.Page = (id, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article(id)], page + 1));
        vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
        _dispatcher.Drain();
        Assert.Equal("2", Assert.Single(vm.Articles).Data.Identifier.Id);
        service.Page = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([], page + 1));
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        var stopped = service.Calls.Count;
        vm.ChangeChildPartitionCommand.Execute(vm.Children[0]);
        _dispatcher.Drain();
        Assert.Equal("initial", Assert.Single(vm.Articles).Data.Identifier.Id);
        vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
        _dispatcher.Drain();
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Equal(stopped, service.Calls.Count);
        Assert.Equal("2", Assert.Single(vm.Articles).Data.Identifier.Id);
        service.Page = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article("sorted")], page + 1));
        var sort = Enum.GetValues<ArticleSortType>().First(s => s != ArticleSortType.Default);
        vm.ChangeSortTypeCommand.Execute(sort);
        _dispatcher.Drain();
        Assert.Equal(("2", sort, 0), service.Calls[^1]);
        Assert.Equal("sorted", Assert.Single(vm.Articles).Data.Identifier.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RapidRoundTripKeepsDisplayedListAndChildCursor(bool cachedChild)
    {
        var service = new Discovery();
        var vm = Create("main", service);
        service.Page = (id, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article(id)], page + 1));
        if (cachedChild)
        {
            vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
            _dispatcher.Drain();
            vm.ChangeChildPartitionCommand.Execute(vm.Children[0]);
            _dispatcher.Drain();
        }

        var calls = service.Calls.Count;
        vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
        vm.ChangeChildPartitionCommand.Execute(vm.Children[0]);
        // Both selections happen before any low-priority switch callback runs.
        _dispatcher.Drain();
        Assert.Equal("initial", Assert.Single(vm.Articles).Data.Identifier.Id);
        Assert.Equal(calls, service.Calls.Count);

        vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
        _dispatcher.Drain();
        Assert.Equal("2", Assert.Single(vm.Articles).Data.Identifier.Id);
        Assert.Equal(calls + (cachedChild ? 0 : 1), service.Calls.Count);
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Equal(("2", ArticleSortType.Default, 1), service.Calls[^1]);
        Assert.Equal("2", Assert.Single(vm.Articles).Data.Identifier.Id);
    }

    [Fact]
    public async Task QueuedSwitchesStartOnlyLatestChildRequest()
    {
        var service = new Discovery();
        var vm = Create("main", service);
        var pending = new TaskCompletionSource<(IReadOnlyList<ArticleInformation>, int)>();
        service.Page = (_, _) => pending.Task;
        var calls = service.Calls.Count;
        vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
        // A load triggered before the switch callback cannot append B to A's list.
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Equal(calls, service.Calls.Count);
        vm.ChangeChildPartitionCommand.Execute(vm.Children[0]);
        vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
        _dispatcher.Drain();
        Assert.Equal(calls + 1, service.Calls.Count);
        Assert.Equal(("2", ArticleSortType.Default, 0), service.Calls[^1]);
        Assert.True(vm.IsArticleLoading);
        _dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => pending.SetResult(([Article("child")], 7)));
        _dispatcher.Drain();
        Assert.Equal("child", Assert.Single(vm.Articles).Data.Identifier.Id);
        Assert.False(vm.IsArticleLoading);
        service.Page = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article("next")], page + 1));
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Equal(7, service.Calls[^1].Page);
        Assert.Equal(new[] { "child", "next" }, vm.Articles.Select(a => a.Data.Identifier.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RapidSwitchesPreserveChildStopAndNoProgressBudget(bool stopped)
    {
        var service = new Discovery();
        var vm = Create("main", service);
        service.Page = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article("child")], page + 1));
        vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
        _dispatcher.Drain();
        if (stopped)
        {
            service.Page = (_, _) => throw new InvalidOperationException("child failure");
        }

        await vm.LoadArticlesCommand.ExecuteAsync(null);
        vm.ChangeChildPartitionCommand.Execute(vm.Children[0]);
        _dispatcher.Drain();
        vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
        vm.ChangeChildPartitionCommand.Execute(vm.Children[0]);
        vm.ChangeChildPartitionCommand.Execute(vm.Children[1]);
        _dispatcher.Drain();
        Assert.Equal("child", Assert.Single(vm.Articles).Data.Identifier.Id);
        var calls = service.Calls.Count;
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Equal(calls + (stopped ? 0 : 1), service.Calls.Count);
        if (!stopped)
        {
            Assert.Equal(2, service.Calls[^1].Page);
        }

        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Equal(calls + (stopped ? 0 : 1), service.Calls.Count);
        Assert.Equal(stopped ? 1 : 0, _errors.Exceptions.Count);
        Assert.False(vm.IsArticleLoading);
    }

    [Theory]
    [InlineData("recommend")]
    [InlineData("main")]
    [InlineData("child")]
    public async Task LateFailureDoesNotStopFreshGeneration(string kind)
    {
        var service = new Discovery();
        var vm = Create(kind, service);
        var pending = new TaskCompletionSource<(IReadOnlyList<ArticleInformation>, int)>();
        service.Page = (_, _) => pending.Task;
        var old = vm.LoadArticlesCommand.ExecuteAsync(null);
        service.Page = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article("fresh")], page + 1));
        vm.RefreshCommand.Execute(null);
        _dispatcher.Drain();
        pending.SetException(new InvalidOperationException("late"));
        await old;
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Equal(1, service.Calls[^1].Page);
        Assert.Equal("fresh", Assert.Single(vm.Articles).Data.Identifier.Id);
        Assert.False(vm.IsArticleLoading);
    }

    [Theory]
    [InlineData("partition")]
    [InlineData("sort")]
    public async Task LatePageCannotChangeReplacementListCursorOrLoadingState(string change)
    {
        var service = new Discovery();
        var vm = Create("child", service);
        var oldPage = new TaskCompletionSource<(IReadOnlyList<ArticleInformation>, int)>();
        service.Page = (_, _) => oldPage.Task;
        var oldLoad = vm.LoadArticlesCommand.ExecuteAsync(null);
        var freshPage = new TaskCompletionSource<(IReadOnlyList<ArticleInformation>, int)>();
        service.Page = (_, _) => freshPage.Task;
        if (change == "partition")
        {
            // Switch to the cached main list, then refresh to replace its cursor.
            vm.ChangeChildPartitionCommand.Execute(vm.Children[0]);
            _dispatcher.Drain();
            vm.RefreshCommand.Execute(null);
        }
        else
        {
            vm.ChangeSortTypeCommand.Execute(Enum.GetValues<ArticleSortType>().First(s => s != ArticleSortType.Default));
        }

        _dispatcher.Drain();
        Assert.True(vm.IsArticleLoading);
        oldPage.SetResult(([Article("stale")], 99));
        await oldLoad;
        Assert.Empty(vm.Articles);
        Assert.True(vm.IsArticleLoading);
        _dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => freshPage.SetResult(([Article("fresh")], 7)));
        _dispatcher.Drain();
        Assert.Equal("fresh", Assert.Single(vm.Articles).Data.Identifier.Id);
        Assert.False(vm.IsArticleLoading);
        service.Page = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article("next")], page + 1));
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Equal(7, service.Calls[^1].Page);
        Assert.Equal(new[] { "fresh", "next" }, vm.Articles.Select(a => a.Data.Identifier.Id));
    }

    [Fact]
    public async Task HotLoadsAllCategoriesOnceAndRefreshRecoversFailure()
    {
        var service = new Discovery();
        var vm = Create("hot", service);
        Assert.Equal(new[] { "hot1", "hot2" }, vm.Articles.Select(a => a.Data.Identifier.Id));
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Equal(2, service.HotCalls);
        service.FailHot = true;
        vm.RefreshCommand.Execute(null);
        _dispatcher.Drain();
        Assert.Empty(vm.Articles);
        await vm.LoadArticlesCommand.ExecuteAsync(null);
        Assert.Single(_errors.Exceptions);
        service.FailHot = false;
        vm.RefreshCommand.Execute(null);
        _dispatcher.Drain();
        Assert.Equal(2, vm.Articles.Count);
        Assert.False(vm.IsArticleLoading);
    }

    private static ArticleInformation Article(string id) => new(new ArticleIdentifier(id, id, null, null));

    private sealed class Discovery : IArticleDiscoveryService
    {
        public Func<string, int, Task<(IReadOnlyList<ArticleInformation>, int)>> Page { get; set; }
            = (_, page) => Task.FromResult<(IReadOnlyList<ArticleInformation>, int)>(([Article("initial")], page + 1));
        public List<(string Id, ArticleSortType Sort, int Page)> Calls { get; } = [];
        public int HotCalls { get; private set; }
        public bool FailHot { get; set; }
        public async Task<(IReadOnlyList<ArticleInformation>, IReadOnlyList<ArticleInformation>?, int)> GetRecommendArticlesAsync(int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            Calls.Add(("_", ArticleSortType.Default, pageNumber));
            var (articles, next) = await Page("_", pageNumber);
            return (articles, null, next);
        }
        public Task<(IReadOnlyList<ArticleInformation>, int)> GetPartitionArticlesAsync(Partition partition, ArticleSortType sortType, int pageNumber = 0, CancellationToken cancellationToken = default)
        {
            Calls.Add((partition.Id, sortType, pageNumber));
            return Page(partition.Id, pageNumber);
        }
        public Task<Dictionary<int, string>> GetHotCategoriesAsync(CancellationToken cancellationToken = default)
            => FailHot ? throw new InvalidOperationException("hot failure") : Task.FromResult(new Dictionary<int, string> { [1] = "one", [2] = "two" });
        public Task<IReadOnlyList<ArticleInformation>> GetHotArticlesAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            HotCalls++;
            return Task.FromResult<IReadOnlyList<ArticleInformation>>([Article($"hot{categoryId}")]);
        }
        public Task<IReadOnlyList<Partition>> GetPartitionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ArticleDetail?> GetArticleContentAsync(ArticleIdentifier identifier, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Errors : ILogger<ArticlePartitionDetailViewModel>
    {
        public List<Exception?> Exceptions { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error) Exceptions.Add(exception);
        }
    }
}
