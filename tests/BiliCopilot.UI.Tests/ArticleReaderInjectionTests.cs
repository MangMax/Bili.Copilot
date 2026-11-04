// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls.Article;
using Richasy.BiliKernel.Models.Article;
using static BiliCopilot.UI.Tests.ArticleReaderFixture;

namespace BiliCopilot.UI.Tests;

public sealed partial class ArticleReaderLatestWinsTests
{
    [Fact]
    public async Task NewContentRemainsVisibleWhenOldTemplateReadCompletesLast()
    {
        var pending = new TaskCompletionSource<(string, string, string)>();
        var reads = 0;
        var navigations = new List<string>();
        var reader = new ArticleReader(() => ++reads == 1 ? pending.Task : Task.FromResult(("$body$", "", "Light")), navigations.Add, () => navigations.Clear());
        var old = reader.LoadContentAsync(Detail(new ArticleIdentifier("1001", "A", null, null)));
        await reader.LoadContentAsync(Detail(new ArticleIdentifier("1002", "B", null, null)));
        pending.SetResult(("$body$", "", "Light"));
        await old;
        Assert.Equal(new[] { "<p>1002</p>" }, navigations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PendingTemplateCannotNavigateAfterOwnershipEnds(bool closeReader)
    {
        var pending = new TaskCompletionSource<(string, string, string)>();
        var navigations = new List<string>();
        var closed = false;
        var service = new Discovery();
        var vm = CreateViewModel(service);
        var article = new ArticleIdentifier("1001", "A", null, null);
        var request = vm.InitializeCommand.ExecuteAsync(article);
        service.Complete("1001", Detail(article));
        await request;
        var generation = vm.CurrentLoadGeneration;
        var reader = new ArticleReader(() => pending.Task, navigations.Add, () => closed = true);
        var load = reader.LoadContentAsync(Detail(new ArticleIdentifier("1001", "A", null, null)), isCurrent: closeReader ? null : () => generation == vm.CurrentLoadGeneration);
        if (closeReader)
        {
            reader.ClearContent();
        }
        else
        {
            // Another page request takes ownership without injecting its content here.
            var next = new ArticleIdentifier("1002", "B", null, null);
            var nextRequest = vm.InitializeCommand.ExecuteAsync(next);
            service.Complete("1002", Detail(next));
            await nextRequest;
        }

        pending.SetResult(("$body$", "", "Light"));
        await load;
        Assert.Empty(navigations);
        Assert.Equal(closeReader, closed);
    }
}
