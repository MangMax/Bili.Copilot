// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Article;

namespace BiliCopilot.UI.Tests;

public sealed partial class ArticleReaderLatestWinsTests
{
    [Fact]
    public async Task FailureCanRetryCurrentArticleAndRestoresContent()
    {
        var service = new Discovery();
        var vm = CreateViewModel(service);
        var article = new ArticleIdentifier("1001", "A", null, null);
        var first = vm.InitializeCommand.ExecuteAsync(article);
        service.Fail("1001", new InvalidOperationException("offline"));
        await first;
        Assert.True(vm.HasLoadFailed);
        Assert.False(vm.IsLoading);
        Assert.False(vm.HasContent);
        Assert.True(vm.RetryCommand.CanExecute(null));
        var retry = vm.RetryCommand.ExecuteAsync(null);
        Assert.False(vm.HasLoadFailed);
        Assert.True(vm.IsLoading);
        Assert.False(vm.RetryCommand.CanExecute(null));
        service.Complete("1001", Detail(article));
        await retry;
        Assert.True(vm.HasContent);
        Assert.False(vm.HasLoadFailed);
        Assert.False(vm.IsLoading);
        Assert.Equal(2, service.ContentRequests);
        Assert.Single(vm.AI.Injected);
    }

    [Fact]
    public async Task RetryTransfersOwnershipAndUnloadRejectsLateResponse()
    {
        var service = new Discovery();
        var vm = CreateViewModel(service);
        var article = new ArticleIdentifier("1001", "A", null, null);
        var first = vm.InitializeCommand.ExecuteAsync(article);
        var owned = vm.CurrentLoadGeneration;
        service.Fail("1001", new InvalidOperationException("offline"));
        await first;
        var transitions = 0;
        vm.LoadRetried += (previous, current) =>
        {
            Assert.Equal(owned, previous);
            owned = current;
            transitions++;
        };
        var retry = vm.RetryCommand.ExecuteAsync(null);
        await vm.RetryCommand.ExecuteAsync(null);
        Assert.Equal(2, service.ContentRequests);
        Assert.Equal(1, transitions);
        Assert.Equal(vm.CurrentLoadGeneration, owned);
        vm.CancelLoad(owned);
        Assert.True(service.TokenFor("1001").IsCancellationRequested);
        service.Complete("1001", Detail(article));
        await retry;
        Assert.Null(vm.Content);
        Assert.False(vm.HasContent);
        Assert.False(vm.HasLoadFailed);
        Assert.False(vm.IsLoading);
        Assert.Empty(vm.AI.Injected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LateOppositeOutcomeCannotOverwriteCurrentState(bool currentSucceeds)
    {
        var service = new Discovery();
        var vm = CreateViewModel(service);
        var a = new ArticleIdentifier("1001", "A", null, null);
        var b = new ArticleIdentifier("1002", "B", null, null);
        var old = vm.InitializeCommand.ExecuteAsync(a);
        var current = vm.InitializeCommand.ExecuteAsync(b);
        if (currentSucceeds)
            service.Complete("1002", Detail(b));
        else
            service.Fail("1002", new InvalidOperationException("B failed"));
        await current;
        if (currentSucceeds)
            service.Fail("1001", new InvalidOperationException("late A"));
        else
            service.Complete("1001", Detail(a));
        await old;
        Assert.Equal(!currentSucceeds, vm.HasLoadFailed);
        Assert.Equal(currentSucceeds, vm.HasContent);
        Assert.False(vm.IsLoading);
        Assert.Equal(currentSucceeds ? "1002" : null, vm.Content?.Identifier.Id);
        Assert.Equal("B", vm.Title);
    }

    [Fact]
    public async Task NewArticleClearsFailureAndCancellationStaysSilent()
    {
        var service = new Discovery(cancelPendingRequests: true);
        var vm = CreateViewModel(service);
        var first = vm.InitializeCommand.ExecuteAsync(new ArticleIdentifier("1001", "A", null, null));
        service.Fail("1001", new InvalidOperationException("offline"));
        await first;
        Assert.True(vm.HasLoadFailed);
        var next = vm.InitializeCommand.ExecuteAsync(new ArticleIdentifier("1002", "B", null, null));
        Assert.False(vm.HasLoadFailed);
        Assert.False(vm.HasContent);
        Assert.False(vm.RetryCommand.CanExecute(null));
        vm.CancelLoad(vm.CurrentLoadGeneration);
        await next;
        Assert.False(vm.HasLoadFailed);
        Assert.False(vm.IsLoading);
        Assert.Null(vm.Content);
    }
}
