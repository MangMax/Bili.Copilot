// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Context;
using BiliCopilot.UI.Tests.Support;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// AI 上下文采集的输入→结果边界：字幕选择与格式化、评论截断、文章纯文本、取消与进度。
/// </summary>
public class AIContextCollectorTests
{
    [Fact]
    public async Task CollectAsync_NoSubtitleMetadata_ReportsUnavailableWithoutThrowing()
    {
        var collector = CreateCollector(new FakeSubtitleSource([], []));

        var result = await collector.CollectAsync(VideoRequest());

        Assert.Equal(AISubtitleStatus.Unavailable, result.SubtitleStatus);
        Assert.Equal(string.Empty, result.SubtitleText);
    }

    [Fact]
    public async Task CollectAsync_EmptySubtitleDetail_ReportsUnavailableWithoutThrowing()
    {
        var collector = CreateCollector(new FakeSubtitleSource([ChineseTrack()], []));

        var result = await collector.CollectAsync(VideoRequest());

        Assert.Equal(AISubtitleStatus.Unavailable, result.SubtitleStatus);
        Assert.Equal(string.Empty, result.SubtitleText);
    }

    [Fact]
    public async Task CollectAsync_MultipleLanguages_PrefersChineseNonAITrack()
    {
        var source = new FakeSubtitleSource(
            [new AISubtitleTrack("sub-en", "English", "https://example.com/en.json", false), ChineseTrack()],
            [new AISubtitleCue(0, 1, "你好")]);
        var collector = CreateCollector(source);

        var result = await collector.CollectAsync(VideoRequest());

        Assert.Equal("sub-zh", source.LastRequestedTrackId);
        Assert.Equal(AISubtitleStatus.Available, result.SubtitleStatus);
    }

    [Fact]
    public async Task CollectAsync_SubtitleDetail_FormatsEachCueAsTimeRangeLine()
    {
        var source = new FakeSubtitleSource(
            [ChineseTrack()],
            [
                new AISubtitleCue(0, 59, "开场"),
                new AISubtitleCue(59, 61, "过渡"),
                new AISubtitleCue(3600, 3661, "一小时"),
            ]);
        var collector = CreateCollector(source);

        var result = await collector.CollectAsync(VideoRequest());

        var expected = string.Join(
            Environment.NewLine,
            "00:00 - 00:59: 开场",
            "00:59 - 01:01: 过渡",
            "01:00:00 - 01:01:01: 一小时");
        Assert.Equal(AISubtitleStatus.Available, result.SubtitleStatus);
        Assert.Equal(expected, result.SubtitleText);
    }

    [Fact]
    public async Task CollectAsync_LongComments_TruncatesAtEightHundredCharactersKeepingSeparator()
    {
        var longComment = new string('字', 393);
        var source = new FakeCommentSource(
        [
            new AIComment(longComment),
            new AIComment(longComment),
            new AIComment("超出"),
        ]);
        var collector = CreateCollector(comments: source);

        var result = await collector.CollectAsync(VideoRequest(includeComments: true));

        Assert.Equal((longComment + "\n------") + (longComment + "\n------"), result.CommentText);
        Assert.Equal(800, result.CommentText.Length);
        Assert.DoesNotContain("超出", result.CommentText);
    }

    [Fact]
    public async Task CollectAsync_ArticleHtml_StripsTagsAndCollapsesWhitespace()
    {
        var collector = CreateCollector();

        var result = await collector.CollectAsync(ArticleRequest("<div>第一段<br/>文本</div>\n<p>   第二段   </p>"));

        Assert.Equal("第一段文本 第二段", result.ArticleText);
        Assert.Equal(AISubtitleStatus.NotRequested, result.SubtitleStatus);
        Assert.Equal("文章标题", result.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p>  </p>")]
    public async Task CollectAsync_EmptyArticle_ThrowsTypedExceptionWithoutCollectingComments(string? html)
    {
        var comments = new FakeCommentSource([new AIComment("评论")]);
        var collector = CreateCollector(comments: comments);

        await Assert.ThrowsAsync<AIArticleContentEmptyException>(
            () => collector.CollectAsync(ArticleRequest(html, includeComments: true)));

        Assert.Equal(0, comments.CallCount);
    }

    [Fact]
    public async Task CollectAsync_TokenAlreadyCanceled_ThrowsWithoutCallingSources()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var subtitle = new FakeSubtitleSource([ChineseTrack()], [new AISubtitleCue(0, 1, "你好")]);
        var comments = new FakeCommentSource([new AIComment("评论")]);
        var collector = CreateCollector(subtitle, comments);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => collector.CollectAsync(VideoRequest(includeComments: true), cancellationToken: cts.Token));

        Assert.Equal(0, subtitle.TrackCallCount);
        Assert.Equal(0, comments.CallCount);
    }

    [Fact]
    public async Task CollectAsync_CanceledDuringSubtitleDetail_ThrowsWithoutReturningPartialContext()
    {
        using var cts = new CancellationTokenSource();
        var subtitle = new FakeSubtitleSource([ChineseTrack()], [new AISubtitleCue(0, 1, "你好")])
        {
            OnGetCues = cts.Cancel,
        };
        var collector = CreateCollector(subtitle);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => collector.CollectAsync(VideoRequest(), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task CollectAsync_CanceledDuringComments_ThrowsWithoutReturningPartialContext()
    {
        using var cts = new CancellationTokenSource();
        var comments = new FakeCommentSource([new AIComment("评论")])
        {
            OnGetComments = cts.Cancel,
        };
        var collector = CreateCollector(comments: comments);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => collector.CollectAsync(VideoRequest(includeComments: true), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task CollectAsync_VideoWithComments_ReportsStagesInOrderAndReturnsBothContexts()
    {
        var progress = new RecordingProgress();
        var subtitle = new FakeSubtitleSource([ChineseTrack()], [new AISubtitleCue(0, 1, "你好")]);
        var comments = new FakeCommentSource([new AIComment("评论")]);
        var collector = CreateCollector(subtitle, comments);

        var result = await collector.CollectAsync(VideoRequest(includeComments: true), progress);

        Assert.Equal(new[] { AIContextStage.SubtitleMetadata, AIContextStage.SubtitleDetail, AIContextStage.Comments }, progress.Stages);
        Assert.Equal("00:00 - 00:01: 你好", result.SubtitleText);
        Assert.Equal("评论\n------", result.CommentText);
    }

    [Fact]
    public async Task CollectAsync_ArticleWithComments_ReportsArticleStageBeforeComments()
    {
        var progress = new RecordingProgress();
        var comments = new FakeCommentSource([new AIComment("评论")]);
        var collector = CreateCollector(comments: comments);

        var result = await collector.CollectAsync(ArticleRequest("<p>正文</p>", includeComments: true), progress);

        Assert.Equal(new[] { AIContextStage.ArticleContent, AIContextStage.Comments }, progress.Stages);
        Assert.Equal("正文", result.ArticleText);
    }

    [Fact]
    public async Task CollectAsync_NoSubtitlesWithComments_StillReturnsCommentContext()
    {
        var subtitle = new FakeSubtitleSource([], []);
        var comments = new FakeCommentSource([new AIComment("评论")]);
        var collector = CreateCollector(subtitle, comments);

        var result = await collector.CollectAsync(VideoRequest(includeComments: true));

        Assert.Equal(AISubtitleStatus.Unavailable, result.SubtitleStatus);
        Assert.Equal("评论\n------", result.CommentText);
    }

    private static AISubtitleTrack ChineseTrack()
        => new("sub-zh", "中文（简体）", "https://example.com/sub.json", false);

    private static AIContextCollector CreateCollector(
        FakeSubtitleSource? subtitle = null,
        FakeCommentSource? comments = null)
        => new(subtitle ?? new FakeSubtitleSource([], []), comments ?? new FakeCommentSource([]));

    private static AIContextRequest ArticleRequest(string? html, bool includeComments = false)
        => new()
        {
            Target = AIContextTarget.Article,
            Id = "cv123456",
            Title = "文章标题",
            HtmlContent = html,
            IncludeComments = includeComments,
        };

    private static AIContextRequest VideoRequest(bool includeComments = false)
        => new()
        {
            Target = AIContextTarget.Video,
            Id = "BV1xx411c7mD",
            PartId = "123",
            Title = "测试视频",
            Description = "视频简介",
            IncludeComments = includeComments,
        };
}
