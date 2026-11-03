// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Chat;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 生成代次的取消与当前代次判定。
/// </summary>
public class AIGenerationTests
{
    /// <summary>
    /// 新请求开始后，旧代次被取消且不再属于当前代次。
    /// </summary>
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

    /// <summary>
    /// 取消代次会使令牌进入取消状态，并退出当前代次。
    /// </summary>
    [Fact]
    public void Cancel_CancelsTokenAndDropsCurrent()
    {
        var coordinator = new AIGenerationCoordinator();
        using var generation = coordinator.Begin();

        generation.Cancel();

        Assert.True(generation.Token.IsCancellationRequested);
        Assert.False(generation.IsCurrent);
    }

    /// <summary>
    /// 释放后的代次不再属于当前代次，且释放可重复调用。
    /// </summary>
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

    /// <summary>
    /// 释放之后再次取消不应抛出异常（迟到的取消路径）。
    /// </summary>
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
