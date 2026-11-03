// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 执行一次 AI 文本请求：捕获配置快照、初始化模型、流式批处理并发布结果。
/// </summary>
/// <remarks>
/// 空响应抛出 <see cref="AIChatEmptyResponseException"/>，初始化失败抛出
/// <see cref="AIChatInitializationException"/>；取消以 <see cref="OperationCanceledException"/>
/// 传播且不发布最终结果。旧代次的迟到分片由 <see cref="AIGeneration.IsCurrent"/> 拦截。
/// </remarks>
internal sealed class AIChatRequestRunner
{
    private readonly IAIChatConfigSnapshotReader _snapshotReader;
    private readonly IAIChatModelClient _modelClient;

    /// <summary>
    /// 初始化 <see cref="AIChatRequestRunner"/> 的新实例。
    /// </summary>
    /// <param name="snapshotReader">配置快照读取端口。</param>
    /// <param name="modelClient">模型客户端端口。</param>
    public AIChatRequestRunner(IAIChatConfigSnapshotReader snapshotReader, IAIChatModelClient modelClient)
    {
        ArgumentNullException.ThrowIfNull(snapshotReader);
        ArgumentNullException.ThrowIfNull(modelClient);
        _snapshotReader = snapshotReader;
        _modelClient = modelClient;
    }

    /// <summary>
    /// 执行一次请求。
    /// </summary>
    /// <param name="generation">本次请求所属的代次。</param>
    /// <param name="prompt">发送给模型的提示词。</param>
    /// <param name="target">流式结果发布目标。</param>
    /// <returns>发布给界面的最终全文。</returns>
    /// <exception cref="OperationCanceledException">请求被取消。</exception>
    /// <exception cref="AIChatInitializationException">模型服务初始化失败。</exception>
    /// <exception cref="AIChatEmptyResponseException">模型返回空响应。</exception>
    public async Task<string> RunAsync(AIGeneration generation, string prompt, AIChatStreamTarget target)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(target);

        var token = generation.Token;
        token.ThrowIfCancellationRequested();

        // 快照在请求开始时捕获一次，请求全程使用同一份配置。
        var snapshot = await _snapshotReader.ReadAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();

        try
        {
            await _modelClient.InitializeAsync(snapshot, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AIChatInitializationException(
                $"{snapshot.Provider} | {snapshot.ModelId} 初始化失败: {ex.Message}", ex);
        }

        token.ThrowIfCancellationRequested();

        var session = new StreamingResponseSession(
            target.Schedule,
            (text, isFinal) =>
            {
                if (generation.IsCurrent)
                {
                    target.Publish(text, isFinal);
                }
            });
        using var registration = token.Register(session.Cancel);
        var received = new System.Text.StringBuilder();
        var hasDelta = false;
        Action<string>? onDelta = null;
        if (snapshot.UseStreaming)
        {
            onDelta = chunk =>
            {
                if (string.IsNullOrEmpty(chunk))
                {
                    return;
                }

                hasDelta = true;
                received.Append(chunk);
                session.Append(chunk);
            };
        }

        try
        {
            var text = await _modelClient.CompleteAsync(snapshot, prompt, onDelta, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var finalText = string.IsNullOrEmpty(text) && hasDelta ? received.ToString() : text;
            if (string.IsNullOrEmpty(finalText))
            {
                throw new AIChatEmptyResponseException(
                    $"{snapshot.Provider} | {snapshot.ModelId} 返回空响应，具体错误请查看日志");
            }

            if (!hasDelta)
            {
                session.Append(finalText);
            }

            session.Complete();
            return finalText;
        }
        finally
        {
            session.Cancel();
        }
    }
}
