// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 模型返回空响应时抛出的类型化异常.
/// </summary>
internal sealed class AIChatEmptyResponseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatEmptyResponseException"/> class.
    /// </summary>
    public AIChatEmptyResponseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatEmptyResponseException"/> class.
    /// </summary>
    /// <param name="message">异常消息，包含 provider 与 model.</param>
    public AIChatEmptyResponseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatEmptyResponseException"/> class.
    /// </summary>
    /// <param name="message">异常消息，包含 provider 与 model.</param>
    /// <param name="innerException">导致空响应的内部异常.</param>
    public AIChatEmptyResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
