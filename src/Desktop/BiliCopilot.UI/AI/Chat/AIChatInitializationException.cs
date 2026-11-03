// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 模型服务初始化失败时抛出的类型化异常。
/// </summary>
internal sealed class AIChatInitializationException : Exception
{
    /// <summary>
    /// 初始化 <see cref="AIChatInitializationException"/> 的新实例。
    /// </summary>
    public AIChatInitializationException()
    {
    }

    /// <summary>
    /// 初始化 <see cref="AIChatInitializationException"/> 的新实例。
    /// </summary>
    /// <param name="message">异常消息，包含 provider 与 model。</param>
    public AIChatInitializationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化 <see cref="AIChatInitializationException"/> 的新实例。
    /// </summary>
    /// <param name="message">异常消息，包含 provider 与 model。</param>
    /// <param name="innerException">初始化失败的原始异常。</param>
    public AIChatInitializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
