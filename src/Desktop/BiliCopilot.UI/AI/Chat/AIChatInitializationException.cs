// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 模型服务初始化失败时抛出的类型化异常.
/// </summary>
internal sealed class AIChatInitializationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatInitializationException"/> class.
    /// </summary>
    public AIChatInitializationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatInitializationException"/> class.
    /// </summary>
    /// <param name="message">异常消息，包含 provider 与 model.</param>
    public AIChatInitializationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatInitializationException"/> class.
    /// </summary>
    /// <param name="message">异常消息，包含 provider 与 model.</param>
    /// <param name="innerException">初始化失败的原始异常.</param>
    public AIChatInitializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
