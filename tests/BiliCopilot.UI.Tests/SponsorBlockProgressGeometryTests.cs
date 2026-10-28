// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockProgressGeometryTests
{
    [Fact]
    public void Map_NoSegments_ReturnsEmpty()
    {
        var markers = SponsorBlockProgressGeometry.Map([], null, 100, 200);

        Assert.Empty(markers);
    }

    [Fact]
    public void Map_NoChapters_MapsSegmentProportionally()
    {
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 10, 20);

        var marker = Assert.Single(SponsorBlockProgressGeometry.Map([segment], null, 100, 200));

        Assert.Equal(0, marker.SegmentIndex);
        Assert.Equal(SponsorBlockCategory.Sponsor, marker.Category);
        Assert.Equal(20, marker.Left, 3);
        Assert.Equal(20, marker.Width, 3);
    }

    [Fact]
    public void Map_ClampsSegmentToDuration()
    {
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 120);

        var marker = Assert.Single(SponsorBlockProgressGeometry.Map([segment], null, 100, 100));

        Assert.Equal(90, marker.Left, 3);
        Assert.Equal(10, marker.Width, 3);
    }

    [Fact]
    public void Map_SegmentBeyondDuration_ProducesNoMarker()
    {
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 120, 150);

        Assert.Empty(SponsorBlockProgressGeometry.Map([segment], null, 100, 200));
    }

    [Fact]
    public void Map_InvalidDurationOrWidth_ReturnsEmpty()
    {
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 10, 20);

        Assert.Empty(SponsorBlockProgressGeometry.Map([segment], null, 0, 200));
        Assert.Empty(SponsorBlockProgressGeometry.Map([segment], null, 100, 0));
    }

    [Fact]
    public void Map_MultiChapter_UsesSameGapsAsChapterSegments()
    {
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 40, 60);
        var chapters = new[]
        {
            new SponsorBlockTimeRange(0, 50),
            new SponsorBlockTimeRange(50, 100),
        };

        var markers = SponsorBlockProgressGeometry.Map([segment], chapters, 100, 202);

        Assert.Equal(2, markers.Count);
        Assert.Equal(80, markers[0].Left, 3);
        Assert.Equal(20, markers[0].Width, 3);
        Assert.Equal(102, markers[1].Left, 3);
        Assert.Equal(20, markers[1].Width, 3);
        Assert.Equal(0, markers[0].SegmentIndex);
        Assert.Equal(0, markers[1].SegmentIndex);
    }

    [Fact]
    public void Map_SegmentCoveringAllChapters_LeavesPixelGapBetweenMarkers()
    {
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 0, 100);
        var chapters = new[]
        {
            new SponsorBlockTimeRange(0, 50),
            new SponsorBlockTimeRange(50, 100),
        };

        var markers = SponsorBlockProgressGeometry.Map([segment], chapters, 100, 202);

        Assert.Equal(2, markers.Count);
        Assert.Equal(0, markers[0].Left, 3);
        Assert.Equal(100, markers[0].Width, 3);
        Assert.Equal(102, markers[1].Left, 3);
        Assert.Equal(100, markers[1].Width, 3);
    }

    [Fact]
    public void Map_InvalidChapters_FallBackToSingleSpan()
    {
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 10, 20);
        var chapters = new[] { new SponsorBlockTimeRange(50, 50) };

        var marker = Assert.Single(SponsorBlockProgressGeometry.Map([segment], chapters, 100, 200));

        Assert.Equal(20, marker.Left, 3);
        Assert.Equal(20, marker.Width, 3);
    }

    [Fact]
    public void Map_MultipleSegments_KeepSegmentOrderAndIndex()
    {
        var first = new SponsorBlockSegment(SponsorBlockCategory.Intro, 0, 5);
        var second = new SponsorBlockSegment(SponsorBlockCategory.Outro, 90, 100);

        var markers = SponsorBlockProgressGeometry.Map([first, second], null, 100, 100);

        Assert.Equal(2, markers.Count);
        Assert.Equal(0, markers[0].SegmentIndex);
        Assert.Equal(SponsorBlockCategory.Intro, markers[0].Category);
        Assert.Equal(1, markers[1].SegmentIndex);
        Assert.Equal(SponsorBlockCategory.Outro, markers[1].Category);
        Assert.Equal(90, markers[1].Left, 3);
        Assert.Equal(10, markers[1].Width, 3);
    }

    [Fact]
    public void Map_UsesSharedSegmentGapAndMinimumWidthConstants()
    {
        // 章节分段与标记共用同一组布局常量，避免两处字面量漂移.
        var segments = new[] { new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 0, 100) };
        var chapters = new[]
        {
            new SponsorBlockTimeRange(0, 50),
            new SponsorBlockTimeRange(50, 100),
        };
        const double width = 200d;

        var markers = SponsorBlockProgressGeometry.Map(segments, chapters, 100, width);

        // 第一段右端与第二段左端之间正好留下一个 SegmentGap.
        var firstRight = markers[0].Left + markers[0].Width;
        Assert.Equal(SponsorBlockProgressGeometry.SegmentGap, markers[1].Left - firstRight, 3);
        Assert.True(markers[0].Width >= SponsorBlockProgressGeometry.MinSegmentWidth);
    }

    [Fact]
    public void Map_SegmentOutsideChapters_IsClampedToChapterRange()
    {
        // 章节只覆盖 0-40 秒，而片段落在 40-60 秒：没有重叠就不应绘制.
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 40, 60);
        var chapters = new[] { new SponsorBlockTimeRange(0, 40) };

        Assert.Empty(SponsorBlockProgressGeometry.Map([segment], chapters, 100, 200));
    }

    [Fact]
    public void MarkerHitTest_OnlyAcceptsVisualBandAndPadding()
    {
        SponsorBlockMarker[] markers = [new(0, SponsorBlockCategory.Sponsor, 10, 20)];
        const double trackTop = 15d;
        const double trackHeight = 4d;

        // 与 ChapterSegmentBar.MarkerHitPadding 保持一致.
        const double padding = 4d;

        // 标记与轨道等高（15..19），命中带向上放宽 4px（11..19）.
        Assert.NotNull(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 15, 11, trackTop, trackHeight, padding, false));
        Assert.NotNull(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 15, 15, trackTop, trackHeight, padding, false));
        Assert.NotNull(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 15, 17.5, trackTop, trackHeight, padding, false));
        Assert.NotNull(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 15, 19, trackTop, trackHeight, padding, false));

        // 命中带不会向轨道下方扩展，轨道下方原有的定位 / 拖拽不受影响.
        Assert.Null(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 15, 19.5, trackTop, trackHeight, padding, false));

        // 命中带之上也不再拦截.
        Assert.Null(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 15, 10.5, trackTop, trackHeight, padding, false));
    }

    [Fact]
    public void MarkerHitTest_ChapterEdgeBand_IsNotPreempted()
    {
        SponsorBlockMarker[] markers = [new(0, SponsorBlockCategory.Sponsor, 10, 20)];

        // 上下边缘带代表“跳到章节起点”，即使指针正好落在标记视觉带内也必须让位.
        Assert.Null(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 15, 16, 15, 4, 2, true));

        // 同样位置、不在边缘带时则命中标记.
        Assert.NotNull(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 15, 16, 15, 4, 2, false));
    }

    [Fact]
    public void MarkerHitTest_OutsideMarkerWidth_ReturnsNull()
    {
        SponsorBlockMarker[] markers = [new(0, SponsorBlockCategory.Sponsor, 10, 20)];

        Assert.Null(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 5, 16, 15, 4, 2, false));
        Assert.Null(SponsorBlockProgressGeometry.HitTestMarker(
            markers, 31, 16, 15, 4, 2, false));
    }
}
