// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 单集权益角标的分类规则.
/// </summary>
public class EpisodeAccessTagClassifierTests
{
    [Theory]
    [InlineData(false, false, false, null, EpisodeAccessTag.Unspecified)]
    [InlineData(false, false, false, "会员", EpisodeAccessTag.Unspecified)]
    [InlineData(true, true, false, null, EpisodeAccessTag.Preview)]
    [InlineData(true, false, false, "预告", EpisodeAccessTag.Preview)]
    [InlineData(true, false, true, null, EpisodeAccessTag.Vip)]
    [InlineData(true, false, false, "会员", EpisodeAccessTag.Vip)]
    [InlineData(true, true, true, "会员", EpisodeAccessTag.Preview)]
    [InlineData(true, false, false, "限免", EpisodeAccessTag.LimitedFree)]
    [InlineData(true, false, false, "", EpisodeAccessTag.Free)]
    [InlineData(true, false, false, null, EpisodeAccessTag.Free)]
    [InlineData(true, false, false, "独家", EpisodeAccessTag.Unspecified)]
    public void Classify_MapsMetadataToTag(
        bool isPgc,
        bool isPreview,
        bool isVip,
        string? badgeText,
        EpisodeAccessTag expected)
    {
        var actual = EpisodeAccessTagClassifier.Classify(isPgc, isPreview, isVip, badgeText);

        Assert.Equal(expected, actual);
    }
}
