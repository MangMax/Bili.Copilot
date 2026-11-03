// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 字幕轨道元数据.
/// </summary>
/// <param name="Id">字幕标识.</param>
/// <param name="LanguageName">语言名称.</param>
/// <param name="Url">字幕地址.</param>
/// <param name="IsAI">是否为 AI 生成字幕.</param>
public sealed record AISubtitleTrack(string Id, string LanguageName, string Url, bool IsAI);
