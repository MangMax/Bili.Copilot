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
/// 标记完全覆盖轨道高度，按时间区间整段染色，重叠处仍能透过半透明看到缓冲与已播放进度。
/// 覆盖层不参与命中测试，控件只在标记的视觉带（整条轨道 + 向上少量 padding）内拦截指针，
/// 不会向轨道下方扩展，章节上下边缘带仍保持原有的跳转 / 拖拽行为。
/// 迷你进度条为纯展示控件（<see cref="IsInteractive"/> 为 <c>false</c>），
/// 只绘制标记，不提供悬浮与点击.
/// </remarks>
public sealed partial class ChapterSegmentBar
{
    /// <summary>
    /// 标记的默认不透明度。标记颜色是低饱和的 Fluent 色板，再叠加半透明，
    /// 避免高饱和色块盖住缓冲 / 已播放进度，也避免在进度条上过于抢眼.
    /// </summary>
    private const double MarkerDefaultOpacity = 0.78d;

    /// <summary>
    /// 悬浮标记的不透明度，让指针下的片段在整条进度条中凸显出来.
    /// </summary>
    private const double MarkerHoverOpacity = 1d;

    /// <summary>
    /// 标记命中带向上额外放宽的像素，让 4px 高的轨道也容易命中；
    /// 下方不扩展，标记不会抢走轨道下方原有的定位 / 拖拽.
    /// </summary>
    private const double MarkerHitPadding = 4d;

    private readonly List<Rectangle> _markerVisuals = [];
    private readonly Dictionary<SponsorBlockCategory, Brush> _markerBrushes = [];
    private List<SponsorBlockMarker> _markers = [];
    private SponsorBlockMarker? _hoveredMarker;

    /// <summary>
    /// 是否已订阅全局颜色变化事件.
    /// </summary>
    private bool _colorSettingsHooked;

    /// <summary>
    /// 当前高亮的标记视觉。只改这一个元素的不透明度，
    /// 指针在标记之间移动时不会重建视觉树.
    /// </summary>
    private Rectangle? _hoveredMarkerVisual;

    /// <summary>
    /// 当前高亮标记所属的类别，退出悬浮时据此还原该类别自己的基准不透明度.
    /// </summary>
    private SponsorBlockCategory _hoveredMarkerCategory;

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
        // 高亮元素随后会在下面的循环里统一回到默认不透明度.
        _hoveredMarker = null;
        _hoveredMarkerVisual = null;
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

            // 复用的视觉元素可能停留在上一次的悬浮态，重建时统一回到该类别的不透明度.
            visual.Opacity = ResolveMarkerOpacity(marker.Category);
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
        _hoveredMarkerVisual = null;
        _pendingMarker = null;
        _lastPopupTime = double.NaN;
        _lastPopupX = double.NaN;

        foreach (var visual in _markerVisuals)
        {
            _markerLayer.Children.Remove(visual);
        }

        _markerVisuals.Clear();
    }

    /// <summary>
    /// 创建一个标记矩形：高度与轨道一致，圆角取轨道圆角，
    /// 因此标记在视觉上就是轨道的一段，而不是浮在轨道上的细线.
    /// </summary>
    private Rectangle CreateMarkerVisual()
    {
        var visual = new Rectangle
        {
            Height = TrackHeight,
            RadiusX = TrackHeight / 2d,
            RadiusY = TrackHeight / 2d,
            Opacity = MarkerDefaultOpacity,
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

        // 与设置页色块共用同一处解析，避免两边口径不一致；
        // 主题按本控件的实际主题取，浅色 / 深色各用各的变体.
        brush = SponsorBlockColorHelper.ResolveCategoryBrush(category, ActualTheme);
        _markerBrushes[category] = brush;
        return brush;
    }

    /// <summary>
    /// 该类别标记的绘制不透明度.
    /// <para>
    /// 主题色板是不透明基色，统一叠一层半透明以免盖住缓冲与已播放进度；
    /// 自定义颜色由用户在取色器里自己决定透明度，不再额外衰减，
    /// 否则用户设的 100% 会显示成 78%，与设置页色块显示的透明度不一致.
    /// </para>
    /// </summary>
    private static double ResolveMarkerOpacity(SponsorBlockCategory category)
        => SponsorBlockColorHelper.IsCustomColorInEffect(category)
            ? MarkerHoverOpacity
            : MarkerDefaultOpacity;

    /// <summary>
    /// 主题变化后重绘标记：浅色 / 深色各用各的色板变体，缓存的画刷必须丢弃.
    /// </summary>
    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        _markerBrushes.Clear();
        RebuildMarkers();
    }

    /// <summary>
    /// 设置页修改颜色后重绘标记：先丢弃按类别缓存的画刷，再按现有片段重建视觉.
    /// <para>
    /// 颜色变化可能来自任意线程，统一派发到 UI 线程；播放位置不受影响，因此无需重新加载视频.
    /// </para>
    /// </summary>
    private void OnSponsorBlockColorChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _markerBrushes.Clear();
            RebuildMarkers();
        });
    }

    /// <summary>
    /// 订阅全局颜色变化。事件是静态的，控件卸载时必须取消订阅，否则会持有已离开视觉树的实例.
    /// </summary>
    private void SubscribeColorSettings()
    {
        if (_colorSettingsHooked)
        {
            return;
        }

        _colorSettingsHooked = true;
        SponsorBlockColorSettings.Changed += OnSponsorBlockColorChanged;

        // 控件在卸载期间收不到颜色变化（例如浮层里的进度条只在悬浮时可见），
        // 重新挂载时必须丢弃上一次留下的画刷缓存，否则会继续画旧颜色.
        _markerBrushes.Clear();
    }

    private void UnsubscribeColorSettings()
    {
        if (!_colorSettingsHooked)
        {
            return;
        }

        _colorSettingsHooked = false;
        SponsorBlockColorSettings.Changed -= OnSponsorBlockColorChanged;
    }

    /// <summary>
    /// 命中标记。横向严格限制在标记范围内，纵向限制在标记视觉带
    ///（整条轨道，向上放宽 <see cref="MarkerHitPadding"/>，不向轨道下方扩展）；章节上下边缘带优先，
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

        // 只切换两个元素的不透明度：先还原上一个，再抬高当前这一个.
        ApplyMarkerHighlight(marker);

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

    /// <summary>
    /// 把悬浮高亮从一个标记视觉移到另一个：只改不透明度，不重建视觉树.
    /// </summary>
    /// <param name="marker">新的悬浮标记；<c>null</c> 表示退出悬浮态.</param>
    private void ApplyMarkerHighlight(SponsorBlockMarker? marker)
    {
        if (_hoveredMarkerVisual is not null)
        {
            // 还原上一个标记：回到它所属类别自己的基准不透明度，而不是统一按主题色处理.
            _hoveredMarkerVisual.Opacity = ResolveMarkerOpacity(_hoveredMarkerCategory);
            _hoveredMarkerVisual = null;
        }

        if (marker is not SponsorBlockMarker value)
        {
            return;
        }

        var index = _markers.IndexOf(value);
        if (index < 0 || index >= _markerVisuals.Count)
        {
            return;
        }

        var visual = _markerVisuals[index];
        visual.Opacity = MarkerHoverOpacity;
        _hoveredMarkerVisual = visual;
        _hoveredMarkerCategory = value.Category;
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
