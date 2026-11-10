// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 关键词高亮的解析与匹配（#196）：把设置里的逗号/分号/换行分隔文本变成可复用的匹配器。
/// </summary>
/// <remarks>
/// 纯逻辑，不读取设置也不依赖 WinUI；关键词在解析时去重，匹配时用忽略大小写的包含比较。
/// 关键词只用于"上屏优先级"，不参与发送、屏蔽或任何写操作。
/// </remarks>
public sealed class LiveDanmakuKeywordMatcher
{
    private static readonly char[] Separators = [',', '，', ';', '；', '\n', '\r', '\t', ' '];

    private readonly string[] _keywords;

    private LiveDanmakuKeywordMatcher(string[] keywords) => _keywords = keywords;

    /// <summary>
    /// Gets 一个不匹配任何文本的匹配器.
    /// </summary>
    public static LiveDanmakuKeywordMatcher Empty { get; } = new([]);

    /// <summary>
    /// Gets 生效的关键词数量.
    /// </summary>
    public int Count => _keywords.Length;

    /// <summary>
    /// 解析设置文本；空文本得到不匹配的匹配器。
    /// </summary>
    /// <param name="text">逗号/分号/换行分隔的关键词.</param>
    /// <returns>匹配器.</returns>
    public static LiveDanmakuKeywordMatcher Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keywords = new List<string>();
        foreach (var part in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (seen.Add(part))
            {
                keywords.Add(part);
            }
        }

        return keywords.Count == 0 ? Empty : new LiveDanmakuKeywordMatcher([.. keywords]);
    }

    /// <summary>
    /// 判断文本是否命中任一关键词（忽略大小写的包含匹配）。
    /// </summary>
    /// <param name="text">弹幕文本.</param>
    /// <returns>命中时为 true.</returns>
    public bool IsMatch(string? text)
    {
        if (string.IsNullOrEmpty(text) || _keywords.Length == 0)
        {
            return false;
        }

        foreach (var keyword in _keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
