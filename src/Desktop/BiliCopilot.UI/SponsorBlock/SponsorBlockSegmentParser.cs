// Copyright (c) Bili Copilot. All rights reserved.

using System.Text.Json;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手接口响应解析器.
/// </summary>
public static class SponsorBlockSegmentParser
{
    /// <summary>
    /// 解析接口返回的平铺片段数组.
    /// </summary>
    /// <param name="json">接口返回的 JSON 文本.</param>
    /// <param name="expectedCid">当前分 P 的 cid，用于过滤其他分 P 的片段.</param>
    /// <returns>按开始时间排序的有效可跳过片段，解析失败时返回空列表.</returns>
    public static IReadOnlyList<SponsorBlockSegment> Parse(string? json, string? expectedCid = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<SponsorBlockSegment>();
        }

        List<SponsorBlockSegmentDto>? items;
        try
        {
            items = JsonSerializer.Deserialize(json, SponsorBlockJsonContext.Default.ListSponsorBlockSegmentDto);
        }
        catch (JsonException)
        {
            return Array.Empty<SponsorBlockSegment>();
        }

        if (items is null || items.Count == 0)
        {
            return Array.Empty<SponsorBlockSegment>();
        }

        var segments = new List<SponsorBlockSegment>(items.Count);
        foreach (var item in items)
        {
            if (item?.Segment is not { Length: >= 2 } range)
            {
                continue;
            }

            var start = range[0];
            var end = range[1];
            if (!SponsorBlockSegment.IsValidRange(start, end)
                || !IsSkipAction(item.ActionType)
                || !MatchesCid(item.Cid, expectedCid))
            {
                continue;
            }

            segments.Add(new SponsorBlockSegment(
                SponsorBlockCategoryParser.Parse(item.Category),
                start,
                end,
                item.Uuid,
                item.VideoDuration));
        }

        segments.Sort(static (left, right) =>
        {
            var result = left.Start.CompareTo(right.Start);
            return result != 0 ? result : left.End.CompareTo(right.End);
        });

        return segments;
    }

    private static bool IsSkipAction(string? actionType)
        => string.IsNullOrWhiteSpace(actionType)
            || string.Equals(actionType.Trim(), "skip", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesCid(JsonElement? cid, string? expectedCid)
    {
        if (string.IsNullOrWhiteSpace(expectedCid) || cid is null)
        {
            return true;
        }

        var value = cid.Value.ValueKind switch
        {
            JsonValueKind.String => cid.Value.GetString(),
            JsonValueKind.Number => cid.Value.GetRawText(),
            _ => null,
        };

        return string.IsNullOrEmpty(value)
            || string.Equals(value, expectedCid, StringComparison.Ordinal);
    }
}
