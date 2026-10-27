// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace BiliCopilot.UI.Controls.Core;

/// <summary>
/// 分段式播放进度条：空降片段标记.
/// </summary>
/// <remarks>
/// 标记绘制在章节分段之上、轨道上半部，下半部仍可看到缓冲与已播放进度。
/// 覆盖层不参与命中测试，控件只在标记的视觉带（轨道上半部 + 少量 padding）内拦截指针，
/// 因此轨道下半部与章节上下边缘带仍保持原有的跳转 / 拖拽行为。
/// 迷你进度条为纯展示控件（<see cref="IsInteractive"/> 为 <c>false</c>），
/// 只绘制标记，不提供悬浮与点击.
/// </remarks>
public sealed partial class ChapterSegmentBar
{
    /// <summary>
    /// 标记高度：只占轨道上半部，避免遮住缓冲 / 已播放层的下半部分.
    /// </summary>
    private const double MarkerHeight = TrackHeight / 2d;

    /// <summary>
    /// 标记命中带向上额外放宽的像素，让 4px 高的轨道也容易命中；
    /// 下方不放宽，轨道下半部完整留给原有的精确定位 / 拖拽.
    /// </summary>
    private const double MarkerHitPadding = 4d;

    private readonly List<Rectangle> _markerVisuals = [];
    private readonly Dictionary<SponsorBlockCategory, Brush> _markerBrushes = [];
    private List<SponsorBlockMarker> _markers = [];
    private SponsorBlockMarker? _hoveredMarker;

    /// <summary>
    /// 按下标记后挂起的点击候选：只有指针未超过拖动阈值就释放时才算点击.
    /// </summary>
    private SponsorBlockMarker? _pendingMarker;

    /// <summary>
    /// 片段列表变化后重建标记。仅在加载完成、切换分 P、章节或尺寸变化时调用，
    /// 播放位置的逐帧更新不会重建视觉树.
    /// </summary>
    private void RebuildMarkers()
    {
        var width = _canvas.ActualWidth;
        _markerLayer.Width = width;
        _markerLayer.Height = LayoutHeight;

        _markers = [.. SponsorBlockProgressGeometry.Map(
            ViewModel?.SponsorBlockSegments,
            ResolveChapterRanges(),
            Duration,
            width)];

        // 列表变化后旧标记的下标可能失效，先收起悬浮态与挂起的点击，下一次移动会重新判定.
        _hoveredMarker = null;
        _pendingMarker = null;
        _lastPopupTime = double.NaN;
        _lastPopupX = double.NaN;

        while (_markerVisuals.Count > _markers.Count)
        {
            var last = _markerVisuals[^1];
            _markerLayer.Children.Remove(last);
            _markerVisuals.RemoveAt(_markerVisuals.Count - 1);
        }

        while (_markerVisuals.Count < _markers.Count)
        {
            _markerVisuals.Add(CreateMarkerVisual());
        }

        var top = (LayoutHeight - TrackHeight) / 2d;
        for (var i = 0; i < _markers.Count; i++)
        {
            var marker = _markers[i];
            var visual = _markerVisuals[i];
            visual.Fill = ResolveMarkerBrush(marker.Category);
            visual.Width = marker.Width;
            Canvas.SetLeft(visual, marker.Left);
            Canvas.SetTop(visual, top);
        }
    }

    /// <summary>
    /// 清空全部标记，用于时长归零或控件尚未完成布局的场景.
    /// </summary>
    private void ClearMarkers()
    {
        if (_markers.Count == 0 && _markerVisuals.Count == 0)
        {
            return;
        }

        _markers = [];
        _hoveredMarker = null;
        _pendingMarker = null;
        _lastPopupTime = double.NaN;
        _lastPopupX = double.NaN;

        foreach (var visual in _markerVisuals)
        {
            _markerLayer.Children.Remove(visual);
        }

        _markerVisuals.Clear();
    }

    private Rectangle CreateMarkerVisual()
    {
        var visual = new Rectangle
        {
            Height = MarkerHeight,
            RadiusX = MarkerHeight / 2d,
            RadiusY = MarkerHeight / 2d,
        };

        _markerLayer.Children.Add(visual);
        return visual;
    }

    /// <summary>
    /// 读取章节区间并转换为纯几何层使用的形式；无章节时返回 <c>null</c>，由几何层退化为单一整段.
    /// </summary>
    private List<SponsorBlockTimeRange>? ResolveChapterRanges()
    {
        var chapters = ViewModel?.Chapters;
        if (chapters is null || chapters.Count == 0)
        {
            return null;
        }

        var duration = Duration;
        var ranges = new List<SponsorBlockTimeRange>(chapters.Count);
        foreach (var chapter in chapters)
        {
            // 与章节分段布局保持一致：结束位置缺失或无效时延伸到视频末尾.
            var end = chapter.EndPosition > chapter.Position ? chapter.EndPosition : duration;
            ranges.Add(new SponsorBlockTimeRange(chapter.Position, end));
        }

        return ranges;
    }

    private Brush ResolveMarkerBrush(SponsorBlockCategory category)
    {
        if (_markerBrushes.TryGetValue(category, out var brush))
        {
            return brush;
        }

        var argb = SponsorBlockCategoryPalette.GetFallbackColor(category);
        var fallback = new SolidColorBrush(Windows.UI.Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb));
        brush = ResolveResource(SponsorBlockCategoryPalette.GetBrushKey(category), fallback);
        _markerBrushes[category] = brush;
        return brush;
    }

    /// <summary>
    /// 命中标记。横向严格限制在标记范围内，纵向限制在标记视觉带
    ///（轨道上半部，向上放宽 <see cref="MarkerHitPadding"/>）；章节上下边缘带优先，
    /// 这样标记之外的位置仍由原有的按位置跳转 / 章节跳转逻辑处理.
    /// </summary>
    /// <param name="x">相对画布的横坐标.</param>
    /// <param name="y">相对画布的纵坐标.</param>
    /// <param name="isOnVerticalEdge">指针是否位于章节边缘热区.</param>
    /// <returns>命中的标记；没有时返回 <c>null</c>.</returns>
    private SponsorBlockMarker? HitTestMarker(double x, double y, bool isOnVerticalEdge)
    {
        if (!IsInteractive || _markers.Count == 0)
        {
            return null;
        }

        var trackTop = (LayoutHeight - TrackHeight) / 2d;
        return SponsorBlockProgressGeometry.HitTestMarker(
            _markers,
            x,
            y,
            trackTop,
            TrackHeight,
            MarkerHitPadding,
            isOnVerticalEdge);
    }

    /// <summary>
    /// 更新当前悬浮的标记。文本先于浮层显示写入，避免出现空白浮层.
    /// </summary>
    private void SetHoveredMarker(SponsorBlockMarker? marker)
    {
        if (_hoveredMarker == marker)
        {
            return;
        }

        _hoveredMarker = marker;

        // 强制下一次更新重新计算浮层文本与位置.
        _lastPopupTime = double.NaN;
        _lastPopupX = double.NaN;

        if (marker is not SponsorBlockMarker value
            || !TryGetSponsorSegment(value.SegmentIndex, out var segment))
        {
            return;
        }

        _hoverTitle.Text = SponsorBlockCategoryHelper.GetDisplayName(value.Category);
        _hoverTitle.Visibility = Visibility.Visible;
        _hoverTime.Text = string.Format(
            ResourceToolkit.GetLocalizedString(StringNames.SponsorBlockMarkerTimeRange),
            FormatTime(segment.Start),
            FormatTime(segment.End));
    }

    private bool TryGetSponsorSegment(int index, out SponsorBlockSegment segment)
    {
        var segments = ViewModel?.SponsorBlockSegments;
        if (segments is not null && index >= 0 && index < segments.Count)
        {
            segment = segments[index];
            return true;
        }

        segment = null!;
        return false;
    }

    private void OnSponsorBlockSegmentsChanged(object? sender, EventArgs e) => RebuildMarkers();
}
