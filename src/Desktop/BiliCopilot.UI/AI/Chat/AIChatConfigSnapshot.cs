// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 一次 AI 请求开始前捕获的配置快照.
/// </summary>
/// <remarks>
/// 快照在请求开始时读取一次，请求全程使用同一份配置；
/// 请求过程中切换服务或模型不会影响在途请求.
/// </remarks>
internal sealed class AIChatConfigSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatConfigSnapshot"/> class.
    /// </summary>
    /// <param name="provider">服务提供方名称，例如 OpenAI.</param>
    /// <param name="modelId">模型标识.</param>
    /// <param name="providerConfig">由适配层解释的不透明服务配置.</param>
    /// <param name="useStreaming">是否以流式方式请求.</param>
    public AIChatConfigSnapshot(string provider, string modelId, object? providerConfig, bool useStreaming)
    {
        ArgumentException.ThrowIfNullOrEmpty(provider);
        ArgumentException.ThrowIfNullOrEmpty(modelId);
        Provider = provider;
        ModelId = modelId;
        ProviderConfig = providerConfig;
        UseStreaming = useStreaming;
    }

    /// <summary>
    /// 获取服务提供方名称.
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// 获取模型标识.
    /// </summary>
    public string ModelId { get; }

    /// <summary>
    /// 获取不透明的服务配置，由模型客户端适配层解释.
    /// </summary>
    public object? ProviderConfig { get; }

    /// <summary>
    /// 获取是否以流式方式请求.
    /// </summary>
    public bool UseStreaming { get; }
}
