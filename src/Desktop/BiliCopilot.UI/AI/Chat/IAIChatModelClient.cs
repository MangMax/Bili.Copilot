// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 模型客户端端口：初始化服务并完成一次文本请求.
/// </summary>
internal interface IAIChatModelClient
{
    /// <summary>
    /// 按快照初始化模型服务.
    /// </summary>
    /// <param name="snapshot">请求开始时的配置快照.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>初始化完成的任务.</returns>
    Task InitializeAsync(AIChatConfigSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成一次文本请求.
    /// </summary>
    /// <param name="snapshot">请求开始时的配置快照.</param>
    /// <param name="prompt">发送给模型的提示词.</param>
    /// <param name="onDelta">流式增量回调；为 <c>null</c> 时表示一次性返回.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>完整响应文本；流式请求返回增量拼接后的全文.</returns>
    Task<string> CompleteAsync(
        AIChatConfigSnapshot snapshot,
        string prompt,
        Action<string>? onDelta,
        CancellationToken cancellationToken = default);
}
