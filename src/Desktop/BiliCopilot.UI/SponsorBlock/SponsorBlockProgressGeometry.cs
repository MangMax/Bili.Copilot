// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 进度条上的时间区间（秒）.
/// </summary>
public readonly record struct SponsorBlockTimeRange(double Start, double End);

/// <summary>
/// 空降片段在进度条上的一段绘制矩形.
/// </summary>
/// <remarks>
/// 片段跨越章节边界时会被拆成多段，各段共用同一个 <see cref="SegmentIndex"/>，
/// 分别落在各自的章节分段内，以免跨过章节之间的像素间隙.
/// </remarks>
/// <param name="SegmentIndex">片段在 <c>SponsorBlockSegments</c> 中的下标.</param>
/// <param name="Category">片段类别，用于选择标记颜色.</param>
/// <param name="Left">相对进度条左边缘的像素位置.</param>
/// <param name="Width">像素宽度.</param>
public readonly record struct SponsorBlockMarker(
    int SegmentIndex,
    SponsorBlockCategory Category,
    double Left,
    double Width);

/// <summary>
/// 空降片段到进度条像素位置的映射.
/// <para>
/// 纯几何计算，不依赖 WinUI。章节分段的左边界、宽度与 <c>ChapterSegmentBar</c>
/// 的章节布局保持一致：首段贴左、末段贴右，相邻分段各让出半个 <see cref="SegmentGap"/>.
/// </para>
/// </summary>
public static class SponsorBlockProgressGeometry
{
    /// <summary>
    /// 相邻章节分段之间的像素间隙，必须与进度条控件保持一致.
    /// </summary>
    public const double SegmentGap = 2d;

    /// <summary>
    /// 章节分段的最小像素宽度，与该进度条章节布局保持一致.
    /// </summary>
    public const double MinSegmentWidth = 2d;

    /// <summary>
    /// 判断指针纵坐标是否落在标记的视觉命中带内。
    /// <para>
    /// 标记与轨道等高，命中带因此也是整条轨道；为了在 4px 高的轨道上仍然好点，
    /// 只向上方放宽 <paramref name="padding"/>，下方不再放宽，
    /// 标记不会抢占轨道下方的指针位置，章节上下边缘带也照常优先.
    /// </para>
    /// </summary>
    /// <param name="y">相对画布的纵坐标.</param>
    /// <param name="trackTop">轨道上边缘.</param>
    /// <param name="trackHeight">轨道高度.</param>
    /// <param name="padding">命中带向上额外放宽的像素.</param>
    /// <returns>位于命中带内时返回 <c>true</c>.</returns>
    public static bool IsWithinMarkerBand(double y, double trackTop, double trackHeight, double padding)
    {
        if (!IsFinite(y) || !IsFinite(trackTop) || !IsFinite(trackHeight)
            || trackHeight <= 0 || padding < 0)
        {
            return false;
        }

        var bandBottom = trackTop + trackHeight;
        return y >= trackTop - padding && y <= bandBottom;
    }

    /// <summary>
    /// 按横坐标查找命中的标记.
    /// </summary>
    /// <param name="markers">当前绘制中的标记.</param>
    /// <param name="x">相对画布的横坐标.</param>
    /// <returns>命中的标记；没有时返回 <c>null</c>.</returns>
    public static SponsorBlockMarker? FindMarkerAt(IReadOnlyList<SponsorBlockMarker>? markers, double x)
    {
        if (markers is null || !IsFinite(x))
        {
            return null;
        }

        foreach (var marker in markers)
        {
            if (x >= marker.Left && x <= marker.Left + marker.Width)
            {
                return marker;
            }
        }

        return null;
    }

    /// <summary>
    /// 判定指针是否命中标记。
    /// <para>
    /// 章节上下边缘带优先：这些位置代表「跳到章节起点」，
    /// 即使横向落在标记范围内也不能被标记抢占.
    /// </para>
    /// </summary>
    /// <param name="markers">当前绘制中的标记.</param>
    /// <param name="x">相对画布的横坐标.</param>
    /// <param name="y">相对画布的纵坐标.</param>
    /// <param name="trackTop">轨道上边缘.</param>
    /// <param name="trackHeight">轨道高度.</param>
    /// <param name="padding">命中带向上额外放宽的像素.</param>
    /// <param name="isOnVerticalEdge">指针是否位于章节边缘热区.</param>
    /// <returns>命中的标记；没有时返回 <c>null</c>.</returns>
    public static SponsorBlockMarker? HitTestMarker(
        IReadOnlyList<SponsorBlockMarker>? markers,
        double x,
        double y,
        double trackTop,
        double trackHeight,
        double padding,
        bool isOnVerticalEdge)
        => isOnVerticalEdge || !IsWithinMarkerBand(y, trackTop, trackHeight, padding)
            ? null
            : FindMarkerAt(markers, x);

    /// <summary>
    /// 计算各片段在进度条上的绘制矩形.
    /// </summary>
    /// <param name="segments">当前分 P 的片段列表.</param>
    /// <param name="chapters">章节区间；为空或全部无效时退化为单一整段.</param>
    /// <param name="duration">视频总时长（秒）.</param>
    /// <param name="width">进度条可用像素宽度.</param>
    /// <returns>
    /// 按片段顺序排列的标记；同一片段可能因跨越章节而出现多次.
    /// 没有可绘制内容时返回空列表.
    /// </returns>
    public static IReadOnlyList<SponsorBlockMarker> Map(
        IReadOnlyList<SponsorBlockSegment>? segments,
        IReadOnlyList<SponsorBlockTimeRange>? chapters,
        double duration,
        double width)
    {
        var markers = new List<SponsorBlockMarker>();
        if (segments is null || segments.Count == 0
            || !IsFinite(duration) || duration <= 0
            || !IsFinite(width) || width <= 0)
        {
            return markers;
        }

        var spans = ResolveSpans(chapters, duration);
        var layout = ResolveLayout(spans, duration, width);

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            if (segment is null || !IsFinite(segment.Start) || !IsFinite(segment.End))
            {
                continue;
            }

            // 片段可能来自不同时长的版本（例如投稿时视频尚未转码完成），
            // 这里按当前时长裁剪，超出部分不绘制.
            var segmentStart = Math.Clamp(segment.Start, 0, duration);
            var segmentEnd = Math.Clamp(segment.End, 0, duration);
            if (segmentEnd <= segmentStart)
            {
                continue;
            }

            for (var i = 0; i < spans.Count; i++)
            {
                var span = spans[i];
                var from = Math.Max(segmentStart, span.Start);
                var to = Math.Min(segmentEnd, span.End);
                if (to <= from)
                {
                    continue;
                }

                var spanLength = span.End - span.Start;
                if (spanLength <= 0)
                {
                    continue;
                }

                var (left, spanWidth) = layout[i];
                var fromRatio = (from - span.Start) / spanLength;
                var toRatio = (to - span.Start) / spanLength;
                var markerWidth = (toRatio - fromRatio) * spanWidth;
                if (markerWidth <= 0)
                {
                    continue;
                }

                markers.Add(new SponsorBlockMarker(
                    index,
                    segment.Category,
                    left + (fromRatio * spanWidth),
                    markerWidth));
            }
        }

        return markers;
    }

    /// <summary>
    /// 解析章节区间：裁剪到视频范围、丢弃无效项；无有效章节时退化为单一整段.
    /// </summary>
    private static List<SponsorBlockTimeRange> ResolveSpans(
        IReadOnlyList<SponsorBlockTimeRange>? chapters,
        double duration)
    {
        var spans = new List<SponsorBlockTimeRange>();
        if (chapters is not null)
        {
            foreach (var chapter in chapters)
            {
                if (!IsFinite(chapter.Start) || !IsFinite(chapter.End))
                {
                    continue;
                }

                var start = Math.Clamp(chapter.Start, 0, duration);
                var end = Math.Clamp(chapter.End, 0, duration);
                if (end > start)
                {
                    spans.Add(new SponsorBlockTimeRange(start, end));
                }
            }
        }

        if (spans.Count == 0)
        {
            spans.Add(new SponsorBlockTimeRange(0, duration));
        }

        return spans;
    }

    /// <summary>
    /// 按章节计算每个分段的像素左边界与宽度，规则与进度条章节布局一致.
    /// </summary>
    private static (double Left, double Width)[] ResolveLayout(
        List<SponsorBlockTimeRange> spans,
        double duration,
        double width)
    {
        var layout = new (double Left, double Width)[spans.Count];
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            var isFirst = i == 0;
            var isLast = i == spans.Count - 1;
            var left = (span.Start / duration * width) + (isFirst ? 0d : SegmentGap / 2d);
            var right = (span.End / duration * width) - (isLast ? 0d : SegmentGap / 2d);
            layout[i] = (left, Math.Max(right - left, MinSegmentWidth));
        }

        return layout;
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);
}
