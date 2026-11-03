// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.AgentKernel;
using Richasy.AgentKernel.Models;

namespace BiliCopilot.UI.Services.AI;

/// <summary>
/// 当前选中的服务与模型，由视图模型提供。
/// </summary>
internal sealed class AIChatConfigSelection
{
    /// <summary>
    /// 初始化 <see cref="AIChatConfigSelection"/> 的新实例。
    /// </summary>
    /// <param name="provider">聊天服务提供方类型。</param>
    /// <param name="model">选中的聊天模型。</param>
    public AIChatConfigSelection(ChatProviderType provider, ChatModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Provider = provider;
        Model = model;
    }

    /// <summary>
    /// 获取聊天服务提供方类型。
    /// </summary>
    public ChatProviderType Provider { get; }

    /// <summary>
    /// 获取选中的聊天模型。
    /// </summary>
    public ChatModel Model { get; }
}
