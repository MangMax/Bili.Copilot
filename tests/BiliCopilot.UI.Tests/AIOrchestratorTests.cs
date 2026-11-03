// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Chat;
using BiliCopilot.UI.AI.Context;
using BiliCopilot.UI.AI.Conversation;
using BiliCopilot.UI.AI.Prompts;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 编排服务：上下文采集、提示词构建、配置快照与请求执行组合后的可观察行为。
/// </summary>
public class AIOrchestratorTests
{
    private const string VideoTemplate = "视频标题：{title}\n视频简介：{description}\n视频字幕：{subtitle}";
    private const string VideoEvaluationTemplate = "视频标题：{title}\n视频字幕：{subtitle}\n热门评论：{comments}";
    private const string ArticleTemplate = "文章标题：{title}\n文章内容：{content}";
    private const string QuestionTemplate = "视频字幕：{subtitle}\n我的问题：{question}";

    [Fact]
    public async Task RunAsync_NoSubtitle_StillGeneratesAndPublishesWarning()
    {
        var subtitle = new FakeSubtitleSource([], []);
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult("总结内容"));
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(subtitle, new FakeCommentSource([]), client, scheduler);
        var states = Record(orchestrator);

        await orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoSummary, VideoTemplate));

        Assert.Empty(states);
        scheduler.Drain();

        var last = states[^1];
        Assert.Equal("总结内容", last.FinalResult);
        Assert.Equal(string.Empty, last.TempResult);
        Assert.False(last.IsGenerating);
        Assert.Null(last.Stage);
        Assert.Equal(AIConversationWarning.NoSubtitle, last.Warning);
        Assert.Equal(AIConversationErrorKind.None, last.ErrorKind);
        Assert.Equal(string.Empty, last.ErrorMessage);
        Assert.Contains("无字幕", client.LastPrompt);
        Assert.Contains("视频标题：测试视频", client.LastPrompt);
        Assert.Contains("视频简介：视频简介", client.LastPrompt);
    }

    [Fact]
    public async Task RunAsync_ArticleEmptyContent_FailsWithoutRequestingModel()
    {
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult("不应调用"));
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(new FakeSubtitleSource([], []), new FakeCommentSource([]), client, scheduler);
        var states = Record(orchestrator);

        await orchestrator.RunAsync(ArticleRequest("<p>   </p>", AIPromptKind.ArticleSummary, ArticleTemplate));
        scheduler.Drain();

        var last = states[^1];
        Assert.Equal(AIConversationErrorKind.ArticleContentEmpty, last.ErrorKind);
        Assert.Equal(string.Empty, last.ErrorMessage);
        Assert.False(last.IsGenerating);
        Assert.Null(last.Stage);
        Assert.False(client.Initialized);
        Assert.Null(client.LastPrompt);
        Assert.Equal(string.Empty, last.FinalResult);
    }

    [Fact]
    public async Task RunAsync_EmptyResponse_ReportsEmptyResponseErrorWithProviderAndModel()
    {
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult(string.Empty));
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(new FakeSubtitleSource([], []), new FakeCommentSource([]), client, scheduler);
        var states = Record(orchestrator);

        await orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoSummary, VideoTemplate));
        scheduler.Drain();

        var last = states[^1];
        Assert.Equal(AIConversationErrorKind.EmptyResponse, last.ErrorKind);
        Assert.Contains("OpenAI", last.ErrorMessage);
        Assert.Contains("gpt-test", last.ErrorMessage);
        Assert.Equal(string.Empty, last.TempResult);
        Assert.Equal(string.Empty, last.FinalResult);
        Assert.False(last.IsGenerating);
        Assert.Null(last.Stage);
    }

    [Fact]
    public async Task RunAsync_InitializationFailure_ReportsInitializationError()
    {
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult("不应调用"))
        {
            InitializationError = new InvalidOperationException("鉴权失败"),
        };
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(new FakeSubtitleSource([], []), new FakeCommentSource([]), client, scheduler);
        var states = Record(orchestrator);

        await orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoSummary, VideoTemplate));
        scheduler.Drain();

        var last = states[^1];
        Assert.Equal(AIConversationErrorKind.InitializationFailure, last.ErrorKind);
        Assert.Contains("鉴权失败", last.ErrorMessage);
        Assert.Equal(string.Empty, last.FinalResult);
        Assert.False(last.IsGenerating);
    }

    [Fact]
    public async Task RunAsync_ChatFailure_ClearsPendingTempResult()
    {
        var client = new FakeChatModelClient((_, _, onDelta, _) =>
        {
            onDelta?.Invoke("部分回答");
            throw new InvalidOperationException("网络中断");
        });
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(
            new FakeSubtitleSource([], []),
            new FakeCommentSource([]),
            client,
            scheduler,
            StreamingSnapshot());
        var states = Record(orchestrator);

        await orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoSummary, VideoTemplate));
        scheduler.Drain();

        var last = states[^1];
        Assert.Equal(AIConversationErrorKind.ChatFailure, last.ErrorKind);
        Assert.Equal("网络中断", last.ErrorMessage);
        Assert.Equal(string.Empty, last.TempResult);
        Assert.Equal(string.Empty, last.FinalResult);
        Assert.False(last.IsGenerating);
    }

    [Fact]
    public async Task RunAsync_Cancel_ClearsStateAndDropsLateChunks()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeChatModelClient(async (_, _, onDelta, _) =>
        {
            onDelta?.Invoke("第一段");
            started.SetResult(true);
            await release.Task;
            onDelta?.Invoke("迟到内容");
            return "第一段迟到内容";
        });
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(
            new FakeSubtitleSource([], []),
            new FakeCommentSource([]),
            client,
            scheduler,
            StreamingSnapshot());
        var states = Record(orchestrator);

        var run = orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoSummary, VideoTemplate));
        await started.Task;
        orchestrator.Cancel();
        scheduler.Drain();
        release.SetResult(true);
        await run;
        scheduler.Drain();

        var last = states[^1];
        Assert.Equal(string.Empty, last.RequestText);
        Assert.Equal(string.Empty, last.TempResult);
        Assert.Equal(string.Empty, last.FinalResult);
        Assert.False(last.IsGenerating);
        Assert.Null(last.Stage);
        Assert.DoesNotContain(states, p => p.FinalResult.Contains("迟到", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_ReplacedGeneration_LateResultsDoNotOverwriteNewAnswer()
    {
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var client = new FakeChatModelClient(async (_, _, onDelta, _) =>
        {
            if (Interlocked.Increment(ref callCount) == 1)
            {
                onDelta?.Invoke("旧回答");
                firstStarted.SetResult(true);
                await releaseFirst.Task;
                onDelta?.Invoke("旧-迟到");
                return "旧-迟到";
            }

            return "新回答";
        });
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(
            new FakeSubtitleSource([], []),
            new FakeCommentSource([]),
            client,
            scheduler,
            StreamingSnapshot());
        var states = Record(orchestrator);

        var first = orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoSummary, VideoTemplate));
        await firstStarted.Task;
        var second = orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoSummary, VideoTemplate));
        await second;
        scheduler.Drain();
        releaseFirst.SetResult(true);
        await first;
        scheduler.Drain();

        var last = states[^1];
        Assert.Equal("新回答", last.FinalResult);
        Assert.False(last.IsGenerating);
        Assert.DoesNotContain(states, p => p.FinalResult.Contains("旧", StringComparison.Ordinal));
        Assert.DoesNotContain(states, p => p.TempResult.Contains("旧", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_ConfigSwitchedMidRequest_KeepsSnapshotCapturedAtStart()
    {
        var initial = new AIChatConfigSnapshot("OpenAI", "gpt-start", new object(), useStreaming: false);
        var switched = new AIChatConfigSnapshot("DeepSeek", "deepseek-chat", new object(), useStreaming: true);
        var reader = new FakeSnapshotReader(initial);
        var client = new FakeChatModelClient((_, _, _, _) =>
        {
            reader.Snapshot = switched;
            return Task.FromResult("开始时回答");
        });
        var scheduler = new QueueScheduler();
        var orchestrator = new AIConversationOrchestrator(
            new AIContextCollector(new FakeSubtitleSource([], []), new FakeCommentSource([])),
            new AIChatRequestRunner(reader, client),
            scheduler);
        var states = Record(orchestrator);

        await orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoSummary, VideoTemplate));
        scheduler.Drain();

        Assert.Equal(1, reader.ReadCount);
        Assert.Same(initial, client.InitializedWith);
        Assert.Same(initial, client.CompletedWith);
        Assert.Equal("开始时回答", states[^1].FinalResult);
    }

    [Fact]
    public async Task RunAsync_VideoEvaluation_CollectsCommentsAndReportsStagesInOrder()
    {
        var comments = new FakeCommentSource([new AIComment("这是热评")]);
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult("评价内容"));
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(
            new FakeSubtitleSource([new AISubtitleTrack("sub-zh", "中文（简体）", "https://example.com/sub.json", false)], [new AISubtitleCue(0, 1, "你好")]),
            comments,
            client,
            scheduler);
        var states = Record(orchestrator);

        await orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoEvaluation, VideoEvaluationTemplate));
        scheduler.Drain();

        Assert.Equal(1, comments.CallCount);
        Assert.Contains("这是热评", client.LastPrompt);
        Assert.Contains("00:00 - 00:01: 你好", client.LastPrompt);
        Assert.Equal(
            [AIConversationStage.SubtitleMetadata, AIConversationStage.SubtitleDetail, AIConversationStage.Comments, AIConversationStage.Generating],
            Stages(states));
        Assert.Null(states[^1].Warning);
        Assert.Equal("评价内容", states[^1].FinalResult);
    }

    [Fact]
    public async Task RunAsync_VideoSummary_DoesNotCollectComments()
    {
        var comments = new FakeCommentSource([new AIComment("这是热评")]);
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult("总结内容"));
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(new FakeSubtitleSource([], []), comments, client, scheduler);
        Record(orchestrator);

        await orchestrator.RunAsync(VideoRequest(AIPromptKind.VideoSummary, VideoTemplate));
        scheduler.Drain();

        Assert.Equal(0, comments.CallCount);
        Assert.DoesNotContain("这是热评", client.LastPrompt);
    }

    [Fact]
    public async Task RunAsync_ArticleSummary_ReportsArticleContentThenSummarizing()
    {
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult("文章总结"));
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(new FakeSubtitleSource([], []), new FakeCommentSource([]), client, scheduler);
        var states = Record(orchestrator);

        await orchestrator.RunAsync(ArticleRequest("<p>第一段<br/>正文</p>", AIPromptKind.ArticleSummary, ArticleTemplate));
        scheduler.Drain();

        Assert.Contains("文章内容：第一段正文", client.LastPrompt);
        Assert.Equal([AIConversationStage.ArticleContent, AIConversationStage.Summarizing], Stages(states));
        Assert.Equal("文章总结", states[^1].FinalResult);
        Assert.Null(states[^1].Warning);
    }

    [Fact]
    public async Task RunAsync_Question_IncludesQuestionAndMirrorsRequestText()
    {
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult("回答内容"));
        var scheduler = new QueueScheduler();
        var orchestrator = CreateOrchestrator(new FakeSubtitleSource([], []), new FakeCommentSource([]), client, scheduler);
        var states = Record(orchestrator);

        await orchestrator.RunAsync(VideoRequest(
            AIPromptKind.VideoQuestion,
            QuestionTemplate,
            question: "讲了什么？",
            requestText: "讲了什么？"));
        scheduler.Drain();

        Assert.Contains("我的问题：讲了什么？", client.LastPrompt);
        Assert.Equal("讲了什么？", states[0].RequestText);
        Assert.True(states[0].IsGenerating);
        Assert.Equal("回答内容", states[^1].FinalResult);
    }

    private static AIConversationOrchestrator CreateOrchestrator(
        IAISubtitleSource subtitle,
        IAICommentSource commentSource,
        IAIChatModelClient client,
        IAIConversationScheduler scheduler,
        AIChatConfigSnapshot? snapshot = null)
        => new(
            new AIContextCollector(subtitle, commentSource),
            new AIChatRequestRunner(new FakeSnapshotReader(snapshot ?? NonStreamingSnapshot()), client),
            scheduler);

    private static AIConversationStage[] Stages(IEnumerable<AIConversationSnapshot> states)
    {
        var stages = new List<AIConversationStage>();
        foreach (var state in states)
        {
            if (state.Stage is { } stage && (stages.Count == 0 || stages[^1] != stage))
            {
                stages.Add(stage);
            }
        }

        return [.. stages];
    }

    private static List<AIConversationSnapshot> Record(AIConversationOrchestrator orchestrator)
    {
        var states = new List<AIConversationSnapshot>();
        orchestrator.StateChanged += (_, e) => states.Add(e.Snapshot);
        return states;
    }

    private static AIChatConfigSnapshot NonStreamingSnapshot()
        => new("OpenAI", "gpt-test", new object(), useStreaming: false);

    private static AIChatConfigSnapshot StreamingSnapshot()
        => new("OpenAI", "gpt-test", new object(), useStreaming: true);

    private static AIConversationRequest VideoRequest(
        AIPromptKind kind,
        string template,
        string? question = null,
        string requestText = "总结《测试视频》的内容")
        => new()
        {
            Kind = kind,
            RequestText = requestText,
            Template = template,
            Question = question,
            Source = new AIConversationSource
            {
                Target = AIContextTarget.Video,
                Id = "BV1xx411c7mD",
                PartId = "123",
                Title = "测试视频",
                Description = "视频简介",
            },
        };

    private static AIConversationRequest ArticleRequest(string html, AIPromptKind kind, string template)
        => new()
        {
            Kind = kind,
            RequestText = "总结《文章标题》的内容",
            Template = template,
            Source = new AIConversationSource
            {
                Target = AIContextTarget.Article,
                Id = "cv123456",
                Title = "文章标题",
                HtmlContent = html,
            },
        };

    private sealed class FakeSnapshotReader(AIChatConfigSnapshot snapshot) : IAIChatConfigSnapshotReader
    {
        public AIChatConfigSnapshot Snapshot { get; set; } = snapshot;

        public int ReadCount { get; private set; }

        public Task<AIChatConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(Snapshot);
        }
    }

    private sealed class FakeChatModelClient(
        Func<AIChatConfigSnapshot, string, Action<string>?, CancellationToken, Task<string>> complete) : IAIChatModelClient
    {
        public AIChatConfigSnapshot? InitializedWith { get; private set; }

        public AIChatConfigSnapshot? CompletedWith { get; private set; }

        public string? LastPrompt { get; private set; }

        public Exception? InitializationError { get; init; }

        public bool Initialized => InitializedWith is not null;

        public Task InitializeAsync(AIChatConfigSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            InitializedWith = snapshot;
            return InitializationError is null
                ? Task.CompletedTask
                : Task.FromException(InitializationError);
        }

        public Task<string> CompleteAsync(
            AIChatConfigSnapshot snapshot,
            string prompt,
            Action<string>? onDelta,
            CancellationToken cancellationToken = default)
        {
            CompletedWith = snapshot;
            LastPrompt = prompt;
            return complete(snapshot, prompt, onDelta, cancellationToken);
        }
    }

    private sealed class QueueScheduler : IAIConversationScheduler
    {
        public Queue<Action> Pending { get; } = new();

        public void Post(Action callback) => Pending.Enqueue(callback);

        public void Schedule(Action callback, CancellationToken cancellationToken) => Pending.Enqueue(callback);

        public void Drain()
        {
            while (Pending.Count > 0)
            {
                Pending.Dequeue()();
            }
        }
    }

    private sealed class FakeSubtitleSource(
        IReadOnlyList<AISubtitleTrack> tracks,
        IReadOnlyList<AISubtitleCue> cues) : IAISubtitleSource
    {
        public Task<IReadOnlyList<AISubtitleTrack>> GetTracksAsync(string videoId, string? partId, CancellationToken cancellationToken = default)
            => Task.FromResult(tracks);

        public Task<IReadOnlyList<AISubtitleCue>> GetCuesAsync(AISubtitleTrack track, CancellationToken cancellationToken = default)
            => Task.FromResult(cues);
    }

    private sealed class FakeCommentSource(IReadOnlyList<AIComment> comments) : IAICommentSource
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<AIComment>> GetHotCommentsAsync(string targetId, AIContextTarget target, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(comments);
        }
    }
}
