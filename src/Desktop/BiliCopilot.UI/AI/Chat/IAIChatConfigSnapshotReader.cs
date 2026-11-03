// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 配置快照读取端口：在每次请求开始前捕获当前服务与模型配置。
/// </summary>
internal interface IAIChatConfigSnapshotReader
{
    /// <summary>
    /// 读取当前配置快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>请求期间使用的配置快照。</returns>
    Task<AIChatConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
