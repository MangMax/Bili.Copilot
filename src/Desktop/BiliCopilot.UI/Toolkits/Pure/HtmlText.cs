// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 展示层文本工具.
/// </summary>
public static class HtmlText
{
    /// <summary>
    /// 解码文本中的 HTML 实体，供 UI 绑定使用.
    /// </summary>
    /// <param name="text">可能包含 HTML 实体的文本.</param>
    /// <returns>解码后的文本；输入为 null 或空时返回空字符串.</returns>
    public static string Decode(string? text)
        => string.IsNullOrEmpty(text) ? string.Empty : WebUtility.HtmlDecode(text);
}
