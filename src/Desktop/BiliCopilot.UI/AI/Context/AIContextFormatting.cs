// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;

namespace BiliCopilot.UI.AI.Context;

/// <summary>
/// 上下文文本的纯格式化规则.
/// </summary>
internal static class AIContextFormatting
{
    /// <summary>
    /// 格式化字幕：每条字幕一行，形如 "hh:mm:ss - hh:mm:ss: 内容".
    /// </summary>
    /// <param name="cues">字幕条目.</param>
    /// <returns>多行字幕文本；没有条目时为空字符串.</returns>
    public static string FormatSubtitles(IReadOnlyList<AISubtitleCue>? cues)
    {
        if (cues is null || cues.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            cues.Select(cue => $"{FormatDuration(TimeSpan.FromSeconds(cue.StartSeconds))} - {FormatDuration(TimeSpan.FromSeconds(cue.EndSeconds))}: {cue.Content}"));
    }

    /// <summary>
    /// 格式化时长，规则与 <c>AppToolkit.FormatDuration</c> 一致：满 1 小时为 hh:mm:ss，否则为 mm:ss.
    /// </summary>
    /// <param name="duration">时长.</param>
    /// <returns>时长文本.</returns>
    public static string FormatDuration(TimeSpan duration)
        => duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// 把文章 HTML 转为纯文本：去除标签、折叠空白并去除首尾空白.
    /// </summary>
    /// <param name="html">文章 HTML.</param>
    /// <returns>纯文本.</returns>
    /// <exception cref="AIArticleContentEmptyException">正文为空.</exception>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new AIArticleContentEmptyException();
        }

        var text = CollapseWhitespace(RemoveTags(html));
        return string.IsNullOrEmpty(text)
            ? throw new AIArticleContentEmptyException()
            : text;
    }

    /// <summary>
    /// 格式化热评：每条评论后追加 "\n------" 分隔行，总长度不超过 800 字符.
    /// </summary>
    /// <param name="comments">热评列表.</param>
    /// <returns>拼接后的评论文本，可能为空字符串.</returns>
    public static string FormatComments(IReadOnlyList<AIComment>? comments)
    {
        var text = string.Empty;
        if (comments is null || comments.Count == 0)
        {
            return text;
        }

        foreach (var comment in comments)
        {
            var item = (comment.Content ?? string.Empty) + "\n------";
            if (text.Length + item.Length > 800)
            {
                break;
            }

            text += item;
        }

        return text;
    }

    private static string RemoveTags(string html)
    {
        var buffer = new char[html.Length];
        var length = 0;
        var inTag = false;
        foreach (var c in html)
        {
            if (c == '<')
            {
                inTag = true;
                continue;
            }

            if (c == '>')
            {
                inTag = false;
                continue;
            }

            if (!inTag)
            {
                buffer[length++] = c;
            }
        }

        return new string(buffer, 0, length);
    }

    private static string CollapseWhitespace(string text)
    {
        var buffer = new char[text.Length];
        var length = 0;
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = length > 0;
                continue;
            }

            if (pendingSpace)
            {
                buffer[length++] = ' ';
                pendingSpace = false;
            }

            buffer[length++] = c;
        }

        return new string(buffer, 0, length);
    }
}