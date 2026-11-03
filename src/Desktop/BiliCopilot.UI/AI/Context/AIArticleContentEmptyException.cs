// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 文章正文为空时抛出的类型化异常，由上层映射为本地化提示.
/// </summary>
internal sealed class AIArticleContentEmptyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIArticleContentEmptyException"/> class.
    /// </summary>
    internal AIArticleContentEmptyException()
        : base("文章正文为空，无法构建 AI 上下文.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIArticleContentEmptyException"/> class.
    /// </summary>
    /// <param name="message">异常信息.</param>
    internal AIArticleContentEmptyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIArticleContentEmptyException"/> class.
    /// </summary>
    /// <param name="message">异常信息.</param>
    /// <param name="innerException">内部异常.</param>
    internal AIArticleContentEmptyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
