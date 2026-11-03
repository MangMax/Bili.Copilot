// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 供 AI 使用的评论.
/// </summary>
/// <param name="Content">评论文本.</param>
internal sealed record AIComment(string Content);
