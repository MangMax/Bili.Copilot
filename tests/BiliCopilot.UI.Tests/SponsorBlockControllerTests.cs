// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Tests;

public class SponsorBlockControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(SponsorBlockCategory.Sponsor, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.SelfPromo, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.ExclusiveAccess, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.Intro, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.Outro, SponsorBlockBehavior.Skip)]
    [InlineData(SponsorBlockCategory.Interaction, SponsorBlockBehavior.ShowButton)]
    [InlineData(SponsorBlockCategory.PoiHighlight, SponsorBlockBehavior.ShowButton)]
    [InlineData(SponsorBlockCategory.Preview, SponsorBlockBehavior.None)]
    [InlineData(SponsorBlockCategory.Padding, SponsorBlockBehavior.None)]
    [InlineData(SponsorBlockCategory.Filler, SponsorBlockBehavior.None)]
    [InlineData(SponsorBlockCategory.MusicOfftopic, SponsorBlockBehavior.None)]
    [InlineData(SponsorBlockCategory.Unknown, SponsorBlockBehavior.None)]
    public void GetDefault_ReturnsDocumentedDefault(SponsorBlockCategory category, SponsorBlockBehavior expected)
        => Assert.Equal(expected, SponsorBlockBehaviorDefaults.GetDefault(category));

    [Fact]
    public void Evaluate_AutoSkip_SeeksToEndAndMarksHandled()
    {
        var controller = CreateController();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        var decision = controller.Evaluate([segment], 100, 900, false, Start);

        Assert.Equal(SponsorBlockActionKind.Skip, decision.Kind);
        Assert.Same(segment, decision.Segment);
        Assert.Equal(110, decision.TargetPosition);

        Assert.False(controller.Evaluate([segment], 100, 900, false, At(300)).IsActionable);
    }

    [Fact]
    public void Evaluate_WithinStartTolerance_Skips()
    {
        var controller = CreateController();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        var decision = controller.Evaluate([segment], 89.7, 900, false, Start);

        Assert.Equal(SponsorBlockActionKind.Skip, decision.Kind);
        Assert.Equal(110, decision.TargetPosition);
    }

    [Fact]
    public void Evaluate_BeforeStartTolerance_DoesNotAct()
    {
        var controller = CreateController();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        Assert.False(controller.Evaluate([segment], 89.4, 900, false, Start).IsActionable);
    }

    [Fact]
    public void Evaluate_AtOrAfterEnd_DoesNotAct()
    {
        var controller = CreateController();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        Assert.False(controller.Evaluate([segment], 110, 900, false, Start).IsActionable);
        Assert.False(controller.Evaluate([segment], 120, 900, false, At(1000)).IsActionable);
    }

    [Fact]
    public void Evaluate_ThrottlesRapidChecks()
    {
        var controller = CreateController();
        SponsorBlockSegment[] segments =
        [
            new(SponsorBlockCategory.Sponsor, 90, 110, "a"),
            new(SponsorBlockCategory.Outro, 300, 320, "b"),
        ];

        Assert.True(controller.Evaluate(segments, 100, 900, false, Start).IsActionable);
        Assert.False(controller.Evaluate(segments, 300, 900, false, At(100)).IsActionable);

        var decision = controller.Evaluate(segments, 300, 900, false, At(300));
        Assert.Equal(SponsorBlockActionKind.Skip, decision.Kind);
        Assert.Equal("b", decision.Segment?.Uuid);
        Assert.Equal(320, decision.TargetPosition);
    }

    [Fact]
    public void Evaluate_ClampsTargetToDuration()
    {
        var controller = CreateController();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Outro, 90, 200, "a");

        var decision = controller.Evaluate([segment], 100, 150, false, Start);

        Assert.Equal(SponsorBlockActionKind.Skip, decision.Kind);
        Assert.Equal(150, decision.TargetPosition);
    }

    [Fact]
    public void Evaluate_TooLittleRemaining_DoesNotSkip()
    {
        var controller = CreateController();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Outro, 90, 100.2, "a");

        Assert.False(controller.Evaluate([segment], 100, 900, false, Start).IsActionable);
    }

    [Fact]
    public void Evaluate_NoneBehavior_DoesNothing()
    {
        var controller = new SponsorBlockController();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Preview, 90, 110, "a");

        var decision = controller.Evaluate([segment], 100, 900, false, Start);

        Assert.False(decision.IsActionable);
        Assert.False(controller.IsMuteApplied);
        Assert.False(controller.IsPromptActive);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void Evaluate_InvalidPosition_DoesNothing(double position)
    {
        var controller = CreateController();
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        Assert.False(controller.Evaluate([segment], position, 900, false, Start).IsActionable);
    }

    [Fact]
    public void Evaluate_NoSegments_DoesNothing()
    {
        var controller = CreateController();

        Assert.False(controller.Evaluate([], 100, 900, false, Start).IsActionable);
    }

    [Fact]
    public void Evaluate_MuteBehavior_MutesAndRestoresPreviousUnmutedState()
    {
        var controller = CreateController(SponsorBlockBehavior.Mute);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        var mute = controller.Evaluate([segment], 100, 900, false, Start);

        Assert.Equal(SponsorBlockActionKind.Mute, mute.Kind);
        Assert.True(mute.MuteState);
        Assert.True(controller.IsMuteApplied);

        Assert.False(controller.Evaluate([segment], 101, 900, true, At(300)).IsActionable);

        var restore = controller.Evaluate([segment], 110, 900, true, At(700));

        Assert.Equal(SponsorBlockActionKind.RestoreMute, restore.Kind);
        Assert.False(restore.MuteState);
        Assert.False(controller.IsMuteApplied);
    }

    [Fact]
    public void Evaluate_MuteBehavior_RestoresPreviousMutedState()
    {
        var controller = CreateController(SponsorBlockBehavior.Mute);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        var mute = controller.Evaluate([segment], 100, 900, true, Start);
        Assert.Equal(SponsorBlockActionKind.Mute, mute.Kind);

        var restore = controller.Evaluate([segment], 110, 900, true, At(700));
        Assert.Equal(SponsorBlockActionKind.RestoreMute, restore.Kind);
        Assert.True(restore.MuteState);
    }

    [Fact]
    public void Evaluate_MuteBehavior_DoesNotRepeatMuteWhileInside()
    {
        var controller = CreateController(SponsorBlockBehavior.Mute);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        Assert.Equal(SponsorBlockActionKind.Mute, controller.Evaluate([segment], 100, 900, false, Start).Kind);
        Assert.False(controller.Evaluate([segment], 101, 900, true, At(300)).IsActionable);
        Assert.False(controller.Evaluate([segment], 102, 900, true, At(600)).IsActionable);
    }

    [Fact]
    public void Evaluate_UserMuteChange_SuppressesMuteAndRestore()
    {
        var controller = CreateController(SponsorBlockBehavior.Mute);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        _ = controller.Evaluate([segment], 100, 900, false, Start);
        controller.NotifyUserMuteChanged();

        Assert.False(controller.Evaluate([segment], 101, 900, false, At(300)).IsActionable);
        Assert.False(controller.Evaluate([segment], 102, 900, false, At(600)).IsActionable);

        var exit = controller.Evaluate([segment], 110, 900, false, At(900));
        Assert.NotEqual(SponsorBlockActionKind.RestoreMute, exit.Kind);
    }

    [Fact]
    public void Evaluate_ShowButton_ShowsOnceAndHidesAtSegmentEnd()
    {
        var controller = CreateController(SponsorBlockBehavior.ShowButton);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Interaction, 90, 110, "a");

        var show = controller.Evaluate([segment], 100, 900, false, Start);
        Assert.Equal(SponsorBlockActionKind.ShowButton, show.Kind);
        Assert.Same(segment, show.Segment);
        Assert.True(controller.IsPromptActive);

        Assert.False(controller.Evaluate([segment], 101, 900, false, At(300)).IsActionable);

        var hide = controller.Evaluate([segment], 110, 900, false, At(700));
        Assert.Equal(SponsorBlockActionKind.HideButton, hide.Kind);
        Assert.False(controller.IsPromptActive);

        // 主动返回同一片段时不再提示.
        Assert.False(controller.Evaluate([segment], 100, 900, false, At(1000)).IsActionable);
    }

    [Fact]
    public void Evaluate_ShowButton_HidesAfterTimeout()
    {
        var controller = new SponsorBlockController(_ => SponsorBlockBehavior.ShowButton, TimeSpan.FromSeconds(2));
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Interaction, 90, 200, "a");

        Assert.Equal(SponsorBlockActionKind.ShowButton, controller.Evaluate([segment], 100, 900, false, Start).Kind);

        var hide = controller.Evaluate([segment], 101, 900, false, At(2100));

        Assert.Equal(SponsorBlockActionKind.HideButton, hide.Kind);
        Assert.False(controller.IsPromptActive);
        Assert.False(controller.Evaluate([segment], 102, 900, false, At(2400)).IsActionable);
    }

    [Fact]
    public void AcceptActivePrompt_SeeksToEndAndDoesNotShowAgain()
    {
        var controller = CreateController(SponsorBlockBehavior.ShowButton);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Interaction, 90, 110, "a");

        _ = controller.Evaluate([segment], 100, 900, false, Start);
        var decision = controller.AcceptActivePrompt(900);

        Assert.Equal(SponsorBlockActionKind.Skip, decision.Kind);
        Assert.Same(segment, decision.Segment);
        Assert.Equal(110, decision.TargetPosition);
        Assert.False(controller.IsPromptActive);

        Assert.False(controller.Evaluate([segment], 100, 900, false, At(300)).IsActionable);
    }

    [Fact]
    public void AcceptActivePrompt_ClampsToDuration()
    {
        var controller = CreateController(SponsorBlockBehavior.ShowButton);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Interaction, 90, 110, "a");

        _ = controller.Evaluate([segment], 100, 900, false, Start);

        Assert.Equal(100, controller.AcceptActivePrompt(100).TargetPosition);
    }

    [Fact]
    public void AcceptActivePrompt_WithoutPrompt_DoesNothing()
    {
        var controller = CreateController(SponsorBlockBehavior.ShowButton);

        Assert.False(controller.AcceptActivePrompt(900).IsActionable);
    }

    [Fact]
    public void DismissActivePrompt_HidesAndDoesNotShowAgain()
    {
        var controller = CreateController(SponsorBlockBehavior.ShowButton);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Interaction, 90, 110, "a");

        _ = controller.Evaluate([segment], 100, 900, false, Start);
        var decision = controller.DismissActivePrompt();

        Assert.Equal(SponsorBlockActionKind.HideButton, decision.Kind);
        Assert.False(controller.IsPromptActive);
        Assert.False(controller.Evaluate([segment], 100, 900, false, At(300)).IsActionable);
    }

    [Fact]
    public void DismissActivePrompt_WithoutPrompt_DoesNothing()
    {
        var controller = CreateController(SponsorBlockBehavior.ShowButton);

        Assert.False(controller.DismissActivePrompt().IsActionable);
    }

    [Fact]
    public void Reset_RestoresMuteAndClearsHandledSegments()
    {
        var controller = CreateController(SponsorBlockBehavior.Mute);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Sponsor, 90, 110, "a");

        _ = controller.Evaluate([segment], 100, 900, false, Start);

        var reset = controller.Reset();

        Assert.Equal(SponsorBlockActionKind.RestoreMute, reset.Kind);
        Assert.False(reset.MuteState);
        Assert.False(controller.IsMuteApplied);

        var again = controller.Evaluate([segment], 100, 900, false, At(1000));
        Assert.Equal(SponsorBlockActionKind.Mute, again.Kind);
    }

    [Fact]
    public void Reset_HidesActivePrompt()
    {
        var controller = CreateController(SponsorBlockBehavior.ShowButton);
        var segment = new SponsorBlockSegment(SponsorBlockCategory.Interaction, 90, 110, "a");

        _ = controller.Evaluate([segment], 100, 900, false, Start);

        var reset = controller.Reset();

        Assert.Equal(SponsorBlockActionKind.HideButton, reset.Kind);
        Assert.False(controller.IsPromptActive);
    }

    [Fact]
    public void Reset_WithoutActiveState_DoesNothing()
    {
        var controller = CreateController();

        Assert.False(controller.Reset().IsActionable);
    }

    [Fact]
    public void Evaluate_OnlyAppliesConfiguredCategoryBehavior()
    {
        var controller = new SponsorBlockController(category =>
            category == SponsorBlockCategory.Sponsor ? SponsorBlockBehavior.Mute : SponsorBlockBehavior.None);
        SponsorBlockSegment[] segments =
        [
            new(SponsorBlockCategory.Intro, 10, 20, "intro"),
            new(SponsorBlockCategory.Sponsor, 90, 110, "sponsor"),
        ];

        Assert.False(controller.Evaluate(segments, 15, 900, false, Start).IsActionable);

        var decision = controller.Evaluate(segments, 100, 900, false, At(300));
        Assert.Equal(SponsorBlockActionKind.Mute, decision.Kind);
        Assert.Equal("sponsor", decision.Segment?.Uuid);
    }

    private static SponsorBlockController CreateController(
        SponsorBlockBehavior behavior = SponsorBlockBehavior.Skip,
        TimeSpan? promptTimeout = null)
        => new(_ => behavior, promptTimeout);

    private static DateTimeOffset At(int milliseconds) => Start.AddMilliseconds(milliseconds);
}
