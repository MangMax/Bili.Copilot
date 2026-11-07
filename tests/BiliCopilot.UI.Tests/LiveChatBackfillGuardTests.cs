// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 补屏有效性判定（回归 #197）：换房后迟到的补屏不得落进新房间的聊天列表。
/// 旧实现用 <c>&amp;&amp;</c> 串联两个代际，换房后聊天代际尚未同步时旧补屏仍会通过。
/// </summary>
public sealed class LiveChatBackfillGuardTests
{
    [Fact]
    public void Backfill_IsCurrent_WhenBothGenerationsMatch()
    {
        Assert.True(LiveChatBackfillGuard.IsCurrent(4, 4, 4));
    }

    [Theory]
    [InlineData(3, 4, 4)]
    [InlineData(4, 3, 4)]
    [InlineData(4, 4, 3)]
    [InlineData(4, 3, 3)]
    public void Backfill_IsStale_WhenAnyGenerationDiffers(int generation, int chatGeneration, int sessionGeneration)
    {
        Assert.False(LiveChatBackfillGuard.IsCurrent(generation, chatGeneration, sessionGeneration));
    }

    [Fact]
    public void Backfill_IsStale_AfterRoomChangeBeforeChatTickSyncs()
    {
        // 换房：会话代际立刻前进，聊天代际要等下一个批量节拍才同步。
        // 这正是旧实现（generation != chat && generation != session）误放行的窗口。
        const int staleBackfillGeneration = 7;
        const int syncedChatGeneration = 7;
        const int newSessionGeneration = 8;

        Assert.False(LiveChatBackfillGuard.IsCurrent(
            staleBackfillGeneration,
            syncedChatGeneration,
            newSessionGeneration));
    }
}
