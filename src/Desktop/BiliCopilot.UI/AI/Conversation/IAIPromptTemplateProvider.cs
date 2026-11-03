// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Prompts;

namespace BiliCopilot.UI.AI.Conversation;

/// <summary>
/// 提示词模板来源端口：默认模板与本地文件读写由适配层负责。
/// </summary>
public interface IAIPromptTemplateProvider
{
    /// <summary>
    /// 获取指定类型的提示词模板。
    /// </summary>
    /// <param name="kind">提示词类型。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提示词模板。</returns>
    Task<string> GetTemplateAsync(AIPromptKind kind, CancellationToken cancellationToken = default);
}
