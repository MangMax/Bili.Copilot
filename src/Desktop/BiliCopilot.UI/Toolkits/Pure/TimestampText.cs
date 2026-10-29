// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;
using System.Text.RegularExpressions;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 文本中的一段内容，可能是普通文本，也可能是可跳转的时间戳.
/// </summary>
/// <param name="Text">段落原文，切分后所有段落的原文按顺序拼接即是输入文本.</param>
/// <param name="Seconds">时间戳对应的跳转秒数；普通文本为 <see langword="null"/>.</param>
public sealed record TimestampTextSegment(string Text, double? Seconds)
{
    /// <summary>
    /// 是否为可跳转的时间戳.
    /// </summary>
    public bool IsTimestamp => Seconds.HasValue;
}

/// <summary>
/// 时间戳文本切分工具.
/// </summary>
/// <remarks>
/// 识别 <c>m:ss</c> / <c>mm:ss</c> 与 <c>h:mm:ss</c> / <c>hh:mm:ss</c>：分钟与秒必须在 0-59 之间，
/// 时间戳两侧不能紧贴数字，<c>00:00</c> / <c>0:00</c> 合法且秒数为 0.
/// 评论与视频简介共用这里的切分结果，避免识别规则出现两套实现.
/// </remarks>
public static class TimestampText
{
    private static readonly Regex TimeStampPattern = new(
        @"(?<!\d)(?:(?<hour>\d{1,2}):(?<minute>[0-5]\d):(?<second>[0-5]\d)|(?<minuteOnly>[0-5]?\d):(?<secondOnly>[0-5]\d))(?!\d)",
        RegexOptions.Compiled);

    /// <summary>
    /// 把文本切分为普通文本段与时间戳段，保持原有顺序与原文.
    /// </summary>
    /// <param name="text">待切分的文本.</param>
    /// <returns>按出现顺序排列的段落集合；输入为 null 或空时返回空集合.</returns>
    public static IReadOnlyList<TimestampTextSegment> Split(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<TimestampTextSegment>();
        }

        var matches = TimeStampPattern.Matches(text);
        if (matches.Count == 0)
        {
            return new[] { new TimestampTextSegment(text, null) };
        }

        var segments = new List<TimestampTextSegment>((matches.Count * 2) + 1);
        var lastIndex = 0;
        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                segments.Add(new TimestampTextSegment(text[lastIndex..match.Index], null));
            }

            segments.Add(new TimestampTextSegment(match.Value, ParseSeconds(match)));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            segments.Add(new TimestampTextSegment(text[lastIndex..], null));
        }

        return segments;
    }

    private static double ParseSeconds(Match match)
    {
        if (!match.Groups["hour"].Success)
        {
            var minutes = int.Parse(match.Groups["minuteOnly"].Value, CultureInfo.InvariantCulture);
            var seconds = int.Parse(match.Groups["secondOnly"].Value, CultureInfo.InvariantCulture);
            return (minutes * 60d) + seconds;
        }

        var hours = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var minuteValue = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
        var secondValue = int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture);
        return (hours * 3600d) + (minuteValue * 60d) + secondValue;
    }
}
