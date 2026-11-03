// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.AgentKernel;
using Richasy.AgentKernel.Models;

namespace BiliCopilot.UI.Services.AI;

/// <summary>
/// 当前选中的服务与模型的可变提供者。
/// </summary>
/// <remarks>
/// 视图模型在选择变化时更新这里，配置快照读取器在请求开始时取值，
/// 避免视图模型与编排器之间形成构造循环依赖。
/// </remarks>
public sealed class AIChatSelectionAccessor
{
    private readonly object _gate = new();
    private AIChatConfigSelection? _current;

    /// <summary>
    /// 获取当前选择；尚未选择服务或模型时为 <see langword="null"/>。
    /// </summary>
    internal AIChatConfigSelection? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// 更新当前选中的服务与模型。
    /// </summary>
    /// <param name="provider">聊天服务提供方类型。</param>
    /// <param name="model">选中的聊天模型。</param>
    public void Update(ChatProviderType provider, ChatModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        lock (_gate)
        {
            _current = new AIChatConfigSelection(provider, model);
        }
    }
}
