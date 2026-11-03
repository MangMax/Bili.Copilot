// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Chat;
using BiliCopilot.UI.Tests.Support;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 模型请求执行器：快照捕获、初始化、流式批处理、空响应与取消。
/// </summary>
public class AIChatRequestRunnerTests
{
    [Fact]
    public async Task RunAsync_EmptyResponse_ThrowsTypedExceptionWithProviderAndModel()
    {
        var snapshot = new AIChatConfigSnapshot("OpenAI", "gpt-test", new object(), useStreaming: false);
        var runner = CreateRunner(snapshot, (_, _, _, _) => Task.FromResult(string.Empty));
        var stream = new QueueStreamTarget();
        using var generation = new AIGenerationCoordinator().Begin();

        var exception = await Assert.ThrowsAsync<AIChatEmptyResponseException>(
            () => runner.RunAsync(generation, "你好", stream.Target));

        Assert.Contains("OpenAI", exception.Message);
        Assert.Contains("gpt-test", exception.Message);
        Assert.Empty(stream.Published);
    }

    [Fact]
    public async Task RunAsync_InitializationFailure_ThrowsTypedExceptionWithInnerError()
    {
        var snapshot = new AIChatConfigSnapshot("DeepSeek", "deepseek-chat", new object(), useStreaming: false);
        var stream = new QueueStreamTarget();
        using var generation = new AIGenerationCoordinator().Begin();
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult("不应调用"))
        {
            InitializationError = new InvalidOperationException("鉴权失败"),
        };
        var runner = new AIChatRequestRunner(new FakeSnapshotReader(snapshot), client);

        var exception = await Assert.ThrowsAsync<AIChatInitializationException>(
            () => runner.RunAsync(generation, "你好", stream.Target));

        Assert.Contains("DeepSeek", exception.Message);
        Assert.Contains("deepseek-chat", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Null(client.CompletedWith);
        Assert.Empty(stream.Published);
    }

    [Fact]
    public async Task RunAsync_Cancellation_PropagatesAndNeverPublishesFinal()
    {
        var snapshot = new AIChatConfigSnapshot("OpenAI", "gpt-test", new object(), useStreaming: true);
        var stream = new QueueStreamTarget();
        using var generation = new AIGenerationCoordinator().Begin();
        var client = new FakeChatModelClient((_, _, onDelta, cancellationToken) =>
        {
            onDelta?.Invoke("部分");
            generation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("部分");
        });
        var runner = new AIChatRequestRunner(new FakeSnapshotReader(snapshot), client);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(generation, "问题", stream.Target));

        stream.Drain();
        Assert.DoesNotContain(stream.Published, p => p.Final);
    }

    [Fact]
    public async Task RunAsync_StreamingDeltas_PublishInOrderAndFinalTextImmediately()
    {
        var snapshot = new AIChatConfigSnapshot("OpenAI", "gpt-test", new object(), useStreaming: true);
        var stream = new QueueStreamTarget();
        using var generation = new AIGenerationCoordinator().Begin();
        var client = new FakeChatModelClient((_, _, onDelta, _) =>
        {
            onDelta?.Invoke("第一段");
            onDelta?.Invoke("第二段");
            stream.Drain();
            onDelta?.Invoke("第三段");
            return Task.FromResult("第一段第二段第三段");
        });
        var runner = new AIChatRequestRunner(new FakeSnapshotReader(snapshot), client);

        var result = await runner.RunAsync(generation, "问题", stream.Target);

        Assert.Equal("第一段第二段第三段", result);
        Assert.Equal(2, stream.Published.Count);
        Assert.Equal(("第一段第二段", false), stream.Published[0]);
        Assert.Equal(("第一段第二段第三段", true), stream.Published[1]);
        stream.Drain();
        Assert.Equal(2, stream.Published.Count);
    }

    [Fact]
    public async Task RunAsync_EmptyDeltas_DoNotScheduleOrPublishAndCountAsEmptyResponse()
    {
        var snapshot = new AIChatConfigSnapshot("OpenAI", "gpt-test", new object(), useStreaming: true);
        var stream = new QueueStreamTarget();
        using var generation = new AIGenerationCoordinator().Begin();
        var client = new FakeChatModelClient((_, _, onDelta, _) =>
        {
            onDelta?.Invoke(string.Empty);
            onDelta?.Invoke(string.Empty);
            return Task.FromResult(string.Empty);
        });
        var runner = new AIChatRequestRunner(new FakeSnapshotReader(snapshot), client);

        await Assert.ThrowsAsync<AIChatEmptyResponseException>(
            () => runner.RunAsync(generation, "问题", stream.Target));

        Assert.Empty(stream.Pending);
        Assert.Empty(stream.Published);
    }

    [Fact]
    public async Task RunAsync_NonStreaming_PublishesSingleFinalAndPassesNoDeltaCallback()
    {
        var snapshot = new AIChatConfigSnapshot("OpenAI", "gpt-test", new object(), useStreaming: false);
        var stream = new QueueStreamTarget();
        using var generation = new AIGenerationCoordinator().Begin();
        var client = new FakeChatModelClient((_, _, _, _) => Task.FromResult("完整回答"));
        var runner = new AIChatRequestRunner(new FakeSnapshotReader(snapshot), client);

        var result = await runner.RunAsync(generation, "问题", stream.Target);

        Assert.Equal("完整回答", result);
        Assert.False(client.ReceivedStreamingCallback);
        Assert.Equal(("完整回答", true), Assert.Single(stream.Published));
        stream.Drain();
        Assert.Single(stream.Published);
    }

    [Fact]
    public async Task RunAsync_ReplacedGeneration_DropsLateChunksAndKeepsNewFinal()
    {
        var snapshot = new AIChatConfigSnapshot("OpenAI", "gpt-test", new object(), useStreaming: true);
        var stream = new QueueStreamTarget();
        var coordinator = new AIGenerationCoordinator();
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var client = new FakeChatModelClient(async (_, _, onDelta, _) =>
        {
            if (Interlocked.Increment(ref callCount) == 1)
            {
                onDelta?.Invoke("旧");
                firstStarted.SetResult(true);
                await releaseFirst.Task;
                onDelta?.Invoke("旧-迟到");
                return "旧-迟到";
            }

            onDelta?.Invoke("新回答");
            return "新回答";
        });
        var runner = new AIChatRequestRunner(new FakeSnapshotReader(snapshot), client);

        var first = coordinator.Begin();
        var firstTask = runner.RunAsync(first, "问题一", stream.Target);
        await firstStarted.Task;

        using var second = coordinator.Begin();
        var secondResult = await runner.RunAsync(second, "问题二", stream.Target);
        releaseFirst.SetResult(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstTask);
        stream.Drain();

        Assert.Equal("新回答", secondResult);
        Assert.Contains(("新回答", true), stream.Published);
        Assert.DoesNotContain(stream.Published, p => p.Text.Contains("旧", StringComparison.Ordinal));
        Assert.Equal(("新回答", true), stream.Published[^1]);
        first.Dispose();
    }

    [Fact]
    public async Task RunAsync_ConfigSwitchMidRequest_UsesSnapshotCapturedAtStart()
    {
        var original = new AIChatConfigSnapshot("OpenAI", "gpt-start", new object(), useStreaming: false);
        var switched = new AIChatConfigSnapshot("DeepSeek", "deepseek-chat", new object(), useStreaming: true);
        var reader = new FakeSnapshotReader(original);
        var stream = new QueueStreamTarget();
        using var generation = new AIGenerationCoordinator().Begin();
        var client = new FakeChatModelClient((_, _, onDelta, _) =>
        {
            reader.Snapshot = switched;
            return Task.FromResult(onDelta is null ? "开始时的回答" : "流式回答");
        });
        var runner = new AIChatRequestRunner(reader, client);

        var result = await runner.RunAsync(generation, "问题", stream.Target);

        Assert.Equal("开始时的回答", result);
        Assert.Equal(1, reader.ReadCount);
        Assert.Same(original, client.InitializedWith);
        Assert.Same(original, client.CompletedWith);
    }

    private static AIChatRequestRunner CreateRunner(
        AIChatConfigSnapshot snapshot,
        Func<AIChatConfigSnapshot, string, Action<string>?, CancellationToken, Task<string>> complete)
        => new(new FakeSnapshotReader(snapshot), new FakeChatModelClient(complete));
}

