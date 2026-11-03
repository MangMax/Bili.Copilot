// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 单条字幕.
/// </summary>
/// <param name="StartSeconds">起始位置（秒）.</param>
/// <param name="EndSeconds">结束位置（秒）.</param>
/// <param name="Content">字幕内容.</param>
public sealed record AISubtitleCue(double StartSeconds, double EndSeconds, string Content);
