// Copyright (c) Bili Copilot. All rights reserved.

using System.Diagnostics;
using BiliCopilot.UI.Live;
using BiliCopilot.UI.Tests.Support;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播事件泵：一次观看一条连接、授权后才算就绪、事件可被订阅、取消会停会话、洪峰不撑爆缓冲。
/// 全部用假的 <see cref="ILiveDanmakuService"/> 驱动，不访问网络。
/// </summary>
public sealed class LiveEventPumpTests
{
    [Fact]
    public async Task Start_ConnectsOnceAndReportsConnected()
    {
        var session = new FakeLiveDanmakuSession("123");
        var service = new FakeLiveDanmakuService
        {
            ConnectOverride = async (_, _) =>
            {
                await Task.Yield();
                return session;
            },
        };
        await using var pump = new LiveEventPump(service);

        var start = pump.StartAsync("123");
        Assert.Equal(LiveDanmakuConnectionState.Connecting, pump.State);
        Assert.True(await start);
        Assert.Equal(1, service.ConnectCount);
        Assert.Equal("123", service.LastRoomId);
        Assert.Equal(LiveDanmakuConnectionState.Connected, pump.State);
        Assert.Equal("123", pump.RoomId);
    }

    [Fact]
    public async Task Start_SecondCallIsRejectedBecauseOneSessionOwnsOneConnection()
    {
        var service = new FakeLiveDanmakuService();
        await using var pump = new LiveEventPump(service);
        Assert.True(await pump.StartAsync("123"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => pump.StartAsync("456"));
        Assert.Equal(1, service.ConnectCount);
    }

    [Fact]
    public async Task Start_WithCancelledToken_DoesNotConnect()
    {
        var service = new FakeLiveDanmakuService();
        await using var pump = new LiveEventPump(service);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.False(await pump.StartAsync("123", cancellation.Token));
        Assert.Equal(0, service.ConnectCount);
        Assert.Equal(LiveDanmakuConnectionState.Closed, pump.State);
    }

    [Fact]
    public async Task Unauthorized_PropagatesAndClosesPump()
    {
        var service = new FakeLiveDanmakuService
        {
            ConnectOverride = (_, _) => throw new LiveDanmakuUnauthorizedException(-101, "denied"),
        };
        await using var pump = new LiveEventPump(service);

        await Assert.ThrowsAsync<LiveDanmakuUnauthorizedException>(() => pump.StartAsync("123"));
        Assert.Equal(LiveDanmakuConnectionState.Closed, pump.State);
    }

    [Fact]
    public async Task Events_AreForwardedFromKernelToChannelAndSubscribers()
    {
        var session = new FakeLiveDanmakuSession("123");
        var service = new FakeLiveDanmakuService { Session = session };
        await using var pump = new LiveEventPump(service);
        var received = new List<LiveDanmakuEvent>();
        pump.EventReceived += (_, e) => received.Add(e);
        Assert.True(await pump.StartAsync("123"));

        session.Emit(TestLiveEvents.Danmaku("hello", guardLevel: 3));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var liveEvent = await pump.Events.ReadAsync(cancellation.Token);

        var danmaku = Assert.IsType<LiveDanmakuReceivedEvent>(liveEvent);
        Assert.Equal("hello", danmaku.Danmaku.Text);
        Assert.Equal(3, danmaku.Danmaku.GuardLevel);
        await TestWait.UntilAsync(() => received.Count == 1);
    }

    [Fact]
    public async Task ConnectionStateEvents_UpdatePumpStateAndRaiseSignal()
    {
        var session = new FakeLiveDanmakuSession("123");
        var service = new FakeLiveDanmakuService { Session = session };
        await using var pump = new LiveEventPump(service);
        var states = new List<LiveDanmakuConnectionState>();
        pump.StateChanged += (_, state) => states.Add(state);
        Assert.True(await pump.StartAsync("123"));

        session.Emit(TestLiveEvents.ConnectionState(LiveDanmakuConnectionState.Reconnecting, "network"));
        await TestWait.UntilAsync(() => pump.State == LiveDanmakuConnectionState.Reconnecting);
        Assert.Equal("network", pump.StateDetail);

        session.Emit(TestLiveEvents.ConnectionState(LiveDanmakuConnectionState.Connected));
        await TestWait.UntilAsync(() => pump.State == LiveDanmakuConnectionState.Connected);

        Assert.Contains(LiveDanmakuConnectionState.Connecting, states);
        Assert.Contains(LiveDanmakuConnectionState.Connected, states);
        Assert.Contains(LiveDanmakuConnectionState.Reconnecting, states);
    }

    [Fact]
    public async Task Stop_CancelsEnumerationAndStopsSession()
    {
        var session = new FakeLiveDanmakuSession("123");
        var service = new FakeLiveDanmakuService { Session = session };
        await using var pump = new LiveEventPump(service);
        Assert.True(await pump.StartAsync("123"));

        await pump.StopAsync();

        Assert.Equal(LiveDanmakuConnectionState.Closed, pump.State);
        Assert.True(session.StopCount >= 1);
        Assert.True(pump.Events.Completion.IsCompleted);
    }

    [Fact]
    public async Task Stop_IsBoundedEvenWhenSessionIgnoresCancellation()
    {
        var session = new StuckLiveDanmakuSession();
        var service = new FakeLiveDanmakuService
        {
            ConnectOverride = (_, _) => Task.FromResult<ILiveDanmakuSession>(session),
        };
        await using var pump = new LiveEventPump(service, stopTimeout: TimeSpan.FromMilliseconds(100));
        Assert.True(await pump.StartAsync("123"));

        var stopwatch = Stopwatch.StartNew();
        await pump.StopAsync();
        stopwatch.Stop();

        session.Release();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"停止耗时 {stopwatch.Elapsed}，超出有界等待。");
        Assert.Equal(LiveDanmakuConnectionState.Closed, pump.State);
    }

    [Fact]
    public async Task Flood_KeepsEventBufferBounded()
    {
        const int capacity = 256;
        var service = new FakeLiveDanmakuService
        {
            ConnectOverride = (_, _) => Task.FromResult<ILiveDanmakuSession>(new FloodingLiveDanmakuSession("123", 100_000)),
        };
        await using var pump = new LiveEventPump(service, capacity);
        Assert.True(await pump.StartAsync("123"));

        await pump.Completion.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.True(pump.Events.Count <= capacity, $"缓冲 {pump.Events.Count} 超出上限 {capacity}。");
        Assert.True(pump.DroppedEvents > 0);
        Assert.Equal(LiveDanmakuConnectionState.Closed, pump.State);
    }

    [Fact]
    public async Task Start_WithBlankRoom_DoesNotConnect()
    {
        var service = new FakeLiveDanmakuService();
        await using var pump = new LiveEventPump(service);

        Assert.False(await pump.StartAsync(" "));
        Assert.Equal(0, service.ConnectCount);
    }
}
