// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Chat;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 生成代次的取消与当前代次判定。
/// </summary>
public class AIGenerationTests
{
    [Fact]
    public void Begin_NewGenerationCancelsPreviousAndBecomesCurrent()
    {
        var coordinator = new AIGenerationCoordinator();
        using var first = coordinator.Begin();
        using var second = coordinator.Begin();

        Assert.True(first.Token.IsCancellationRequested);
        Assert.False(first.IsCurrent);
        Assert.True(second.IsCurrent);
    }

    [Fact]
    public void Cancel_CancelsTokenAndDropsCurrent()
    {
        var coordinator = new AIGenerationCoordinator();
        using var generation = coordinator.Begin();

        generation.Cancel();

        Assert.True(generation.Token.IsCancellationRequested);
        Assert.False(generation.IsCurrent);
    }

    [Fact]
    public void Dispose_ReleasesGenerationAndIsIdempotent()
    {
        var coordinator = new AIGenerationCoordinator();
        var generation = coordinator.Begin();

        generation.Dispose();
        generation.Dispose();

        Assert.False(generation.IsCurrent);
        using var replacement = coordinator.Begin();
        Assert.True(replacement.IsCurrent);
        Assert.False(generation.IsCurrent);
    }

    [Fact]
    public void Cancel_AfterDispose_DoesNotThrow()
    {
        var coordinator = new AIGenerationCoordinator();
        var generation = coordinator.Begin();
        generation.Dispose();

        generation.Cancel();

        Assert.False(generation.IsCurrent);
    }
}