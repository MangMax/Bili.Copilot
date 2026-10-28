// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockLoadGuardTests
{
    [Fact]
    public void ShouldApplyResult_EnabledAndSameGeneration_ReturnsTrue()
        => Assert.True(SponsorBlockLoadGuard.ShouldApplyResult(3, 3, true));

    [Fact]
    public void ShouldApplyResult_FeatureDisabled_ReturnsFalse()
        => Assert.False(SponsorBlockLoadGuard.ShouldApplyResult(3, 3, false));

    [Fact]
    public void ShouldApplyResult_GenerationChanged_ReturnsFalse()
        => Assert.False(SponsorBlockLoadGuard.ShouldApplyResult(3, 4, true));
}
