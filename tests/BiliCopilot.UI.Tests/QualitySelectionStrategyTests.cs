// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Resolvers;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 清晰度选择策略：权益过滤、偏好命中与回退规则。
/// </summary>
public class QualitySelectionStrategyTests
{
    [Fact]
    public void Select_NonAuthorCannotSelectLockedFormat_FallsBackToHighestEnabled()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(120, false), new(116, true), new(80, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: PreferQualityType.High,
            LastSelectedQuality: 0);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(116, selection.Quality);
        Assert.Equal(QualitySelectionReason.FallbackHighest, selection.Reason);
    }

    [Fact]
    public void Select_NonAuthorPreferredQualityIsLocked_IgnoresItAndFallsBackToHighestEnabled()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(120, false), new(80, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 120,
            Setting: PreferQualityType.Auto,
            LastSelectedQuality: 120);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(80, selection.Quality);
        Assert.Equal(QualitySelectionReason.FallbackHighest, selection.Reason);
    }

    [Fact]
    public void Select_VideoAuthorCanSelectLockedFormat()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(120, false), new(116, true)],
            Entitlement: QualityEntitlement.VideoAuthor,
            PreferredQuality: 120,
            Setting: PreferQualityType.High,
            LastSelectedQuality: 0);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(120, selection.Quality);
        Assert.Equal(QualitySelectionReason.PreferredQuality, selection.Reason);
    }

    [Fact]
    public void Select_PreferredQualityWinsOverSetting()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(120, true), new(116, true), new(80, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 80,
            Setting: PreferQualityType.UHD,
            LastSelectedQuality: 116);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(80, selection.Quality);
        Assert.Equal(QualitySelectionReason.PreferredQuality, selection.Reason);
    }

    [Fact]
    public void Select_NullPreferredQuality_SkipsToSetting()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(120, true), new(116, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: null,
            Setting: PreferQualityType.HD,
            LastSelectedQuality: 120);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(116, selection.Quality);
        Assert.Equal(QualitySelectionReason.PreferHd, selection.Reason);
    }

    [Fact]
    public void Select_AutoSettingUsesLastSelectedQuality()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(120, true), new(116, true), new(80, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: PreferQualityType.Auto,
            LastSelectedQuality: 116);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(116, selection.Quality);
        Assert.Equal(QualitySelectionReason.LastSelectedQuality, selection.Reason);
    }

    [Fact]
    public void Select_AutoSettingWithoutHistoryFallsBackToHighest()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(116, true), new(80, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: PreferQualityType.Auto,
            LastSelectedQuality: 0);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(116, selection.Quality);
        Assert.Equal(QualitySelectionReason.FallbackHighest, selection.Reason);
    }

    [Fact]
    public void Select_UhdSettingPicks4K()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(116, true), new(80, true), new(120, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: PreferQualityType.UHD,
            LastSelectedQuality: 80);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(120, selection.Quality);
        Assert.Equal(QualitySelectionReason.PreferUhd, selection.Reason);
    }

    [Fact]
    public void Select_UhdSettingUnavailableFallsBackToHighest()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(116, true), new(80, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: PreferQualityType.UHD,
            LastSelectedQuality: 0);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(116, selection.Quality);
        Assert.Equal(QualitySelectionReason.FallbackHighest, selection.Reason);
    }

    [Fact]
    public void Select_HdSettingPrefers116Over80()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(80, true), new(120, true), new(116, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: PreferQualityType.HD,
            LastSelectedQuality: 120);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(116, selection.Quality);
        Assert.Equal(QualitySelectionReason.PreferHd, selection.Reason);
    }

    [Fact]
    public void Select_HdSettingFallsBackTo80When116Unavailable()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(120, true), new(80, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: PreferQualityType.HD,
            LastSelectedQuality: 0);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(80, selection.Quality);
        Assert.Equal(QualitySelectionReason.PreferHd, selection.Reason);
    }

    [Fact]
    public void Select_HighSettingPicksHighestEnabled()
    {
        // “最高画质”是显式分支：即使上次播放记录更低，也取最高可用画质。
        var request = new QualitySelectionRequest(
            Formats: [new(116, true), new(120, true), new(80, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: PreferQualityType.High,
            LastSelectedQuality: 80);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(120, selection.Quality);
        Assert.Equal(QualitySelectionReason.FallbackHighest, selection.Reason);
    }

    [Fact]
    public void Select_UnknownSettingFallsBackToHighest()
    {
        // 未来新增的枚举值必须显式决定语义；未指定时按最高可用画质处理。
        var request = new QualitySelectionRequest(
            Formats: [new(116, true), new(80, true)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: (PreferQualityType)99,
            LastSelectedQuality: 80);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(116, selection.Quality);
        Assert.Equal(QualitySelectionReason.FallbackHighest, selection.Reason);
    }

    [Fact]
    public void Select_EmptyFormats_ReturnsNoSelectionAndReason()
    {
        var request = new QualitySelectionRequest(
            Formats: [],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 0,
            Setting: PreferQualityType.Auto,
            LastSelectedQuality: 0);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.False(selection.HasSelection);
        Assert.Null(selection.Quality);
        Assert.Equal(QualitySelectionReason.NoFormats, selection.Reason);
    }

    [Fact]
    public void Select_AllFormatsLockedForNonAuthor_ReturnsNoSelectionAndReason()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(120, false), new(116, false)],
            Entitlement: QualityEntitlement.Standard,
            PreferredQuality: 120,
            Setting: PreferQualityType.Auto,
            LastSelectedQuality: 116);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.False(selection.HasSelection);
        Assert.Equal(QualitySelectionReason.NoEligibleFormats, selection.Reason);
    }

    [Fact]
    public void Select_DuplicateQuality_ReturnsFirstOccurrence()
    {
        var request = new QualitySelectionRequest(
            Formats: [new(116, true), new(116, false)],
            Entitlement: QualityEntitlement.VideoAuthor,
            PreferredQuality: 0,
            Setting: PreferQualityType.Auto,
            LastSelectedQuality: 116);

        var selection = QualitySelectionStrategy.Select(request);

        Assert.Equal(new QualityCandidate(116, true), selection.Selected);
        Assert.Equal(QualitySelectionReason.LastSelectedQuality, selection.Reason);
    }
}
