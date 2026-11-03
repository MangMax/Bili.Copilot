// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Chat;
using Microsoft.Extensions.AI;
using Richasy.AgentKernel.Chat;
using Richasy.AgentKernel.Models;

namespace BiliCopilot.UI.Services.AI;

/// <summary>
/// 基于 AgentKernel 聊天服务与 Microsoft.Extensions.AI 的模型客户端适配器.
/// </summary>
/// <remarks>
/// 初始化语义与原视图模型一致：调用 <see cref="IChatService.Initialize(AIServiceConfig)"/>，
/// 失败时由请求执行器包装为类型化异常.流式请求把增量交给回调并返回拼接后的全文.
/// </remarks>
internal sealed class AgentKernelChatModelClient : IAIChatModelClient
{
    private readonly Func<string, IChatService> _serviceResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentKernelChatModelClient"/> class.
    /// </summary>
    /// <param name="serviceResolver">按提供方名称解析聊天服务的委托.</param>
    public AgentKernelChatModelClient(Func<string, IChatService> serviceResolver)
    {
        ArgumentNullException.ThrowIfNull(serviceResolver);
        _serviceResolver = serviceResolver;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(AIChatConfigSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var config = snapshot.ProviderConfig as AIServiceConfig
            ?? throw new InvalidOperationException($"{snapshot.Provider} | {snapshot.ModelId} 缺少可用的服务配置");
        ResolveService(snapshot).Initialize(config);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(
        AIChatConfigSnapshot snapshot,
        string prompt,
        Action<string>? onDelta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var service = ResolveService(snapshot);
        var client = service.Client ?? throw new InvalidOperationException($"{snapshot.Provider} | {snapshot.ModelId} 聊天客户端未初始化");
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt),
        };
        var options = new ChatOptions
        {
            ModelId = snapshot.ModelId,
        };

        if (onDelta is null)
        {
            var response = await client.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            return response.Text ?? string.Empty;
        }

        var content = new StringBuilder();
        await foreach (var partialResponse in client.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(partialResponse.Text))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            content.Append(partialResponse.Text);
            onDelta.Invoke(partialResponse.Text);
        }

        return content.ToString();
    }

    private IChatService ResolveService(AIChatConfigSnapshot snapshot)
        => _serviceResolver(snapshot.Provider)
           ?? throw new InvalidOperationException($"{snapshot.Provider} | {snapshot.ModelId} 未找到聊天服务");
}
