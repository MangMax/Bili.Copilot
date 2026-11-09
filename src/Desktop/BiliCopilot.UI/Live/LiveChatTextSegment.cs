// Copyright (c) Bili Copilot. All rights reserved.

using System.Text.RegularExpressions;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 聊天正文的一段：纯文本或表情图片.
/// </summary>
/// <param name="Text">原始文本（表情为 [名称]）.</param>
/// <param name="EmoteUrl">表情地址；纯文本为 null.</param>
/// <param name="EmoteWidth">表情原始宽度.</param>
/// <param name="EmoteHeight">表情原始高度.</param>
public sealed partial record LiveChatTextSegment(string Text, string? EmoteUrl, int EmoteWidth, int EmoteHeight)
{
    /// <summary>
    /// Gets a value indicating whether 这一段是表情图片.
    /// </summary>
    public bool IsEmote => !string.IsNullOrEmpty(EmoteUrl);

    /// <summary>
    /// Gets 表情在聊天行内的显示边长；按原始比例缩放到 20–32px.
    /// </summary>
    public double EmoteSize
    {
        get
        {
            if (!IsEmote)
            {
                return 0;
            }

            var height = EmoteHeight > 0 ? EmoteHeight : 20;
            var width = EmoteWidth > 0 ? EmoteWidth : height;
            var scale = 20d / Math.Min(width, height);
            return Math.Clamp(Math.Max(width, height) * scale, 20d, 32d);
        }
    }

    /// <summary>
    /// 把正文拆成文本与表情段；未识别的 [名称] 保留为文本.
    /// </summary>
    /// <param name="text">正文.</param>
    /// <param name="emotes">接口返回的表情索引.</param>
    /// <returns>分段列表.</returns>
    public static IReadOnlyList<LiveChatTextSegment> Parse(
        string? text,
        IReadOnlyDictionary<string, LiveDanmakuEmote>? emotes)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<LiveChatTextSegment>();
        }

        if (emotes is null || emotes.Count == 0)
        {
            return new[] { new LiveChatTextSegment(text, null, 0, 0) };
        }

        var result = new List<LiveChatTextSegment>();
        var position = 0;
        foreach (Match match in EmotePattern().Matches(text))
        {
            if (match.Index > position)
            {
                result.Add(new LiveChatTextSegment(text[position..match.Index], null, 0, 0));
            }

            var name = match.Groups[1].Value;
            if (emotes.TryGetValue(name, out var emote))
            {
                result.Add(new LiveChatTextSegment(match.Value, emote.Url, emote.Width, emote.Height));
            }
            else
            {
                result.Add(new LiveChatTextSegment(match.Value, null, 0, 0));
            }

            position = match.Index + match.Length;
        }

        if (position < text.Length)
        {
            result.Add(new LiveChatTextSegment(text[position..], null, 0, 0));
        }

        return result;
    }

    [GeneratedRegex(@"\[([^\[\]]+)\]")]
    private static partial Regex EmotePattern();
}
