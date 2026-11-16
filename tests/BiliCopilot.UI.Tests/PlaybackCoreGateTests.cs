// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Player;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// MpvClient.Shutdown 之后，发往该会话的一切命令必须在入口短路（#225）。
/// </summary>
public sealed class PlaybackCoreGateTests
{
    [Fact]
    public void LiveSession_AllowsCommands()
    {
        var gate = new PlaybackCoreGate();

        Assert.False(gate.IsCoreDead);
        Assert.False(gate.ShouldShortCircuit(clientDisposed: false));
    }

    [Fact]
    public void Shutdown_MarksSessionDead_AndShortCircuitsCommands()
    {
        var gate = new PlaybackCoreGate();

        Assert.True(gate.MarkDead());

        Assert.True(gate.IsCoreDead);
        // 客户端尚未 Dispose（Shutdown 由 mpv 自己触发，IsDisposed 仍是 false），
        // 但命令依然必须短路——这是死标志存在的意义。
        Assert.True(gate.ShouldShortCircuit(clientDisposed: false));
        Assert.True(gate.ShouldShortCircuit(clientDisposed: true));
        Assert.True(gate.ShouldShortCircuit(clientDisposed: null));
    }

    [Fact]
    public void Shutdown_IsIdempotent()
    {
        var gate = new PlaybackCoreGate();

        Assert.True(gate.MarkDead());
        Assert.False(gate.MarkDead());
        Assert.True(gate.IsCoreDead);
    }

    [Fact]
    public void DisposedClient_ShortCircuitsWithoutShutdown()
    {
        var gate = new PlaybackCoreGate();

        Assert.True(gate.ShouldShortCircuit(clientDisposed: true));
        Assert.True(gate.ShouldShortCircuit(clientDisposed: null));
        Assert.False(gate.ShouldShortCircuit(clientDisposed: false));
    }

    [Fact]
    public void Reset_AfterRebuild_AllowsCommandsAgain()
    {
        var gate = new PlaybackCoreGate();
        gate.MarkDead();

        gate.Reset();

        Assert.False(gate.IsCoreDead);
        Assert.False(gate.ShouldShortCircuit(clientDisposed: false));
    }
}
