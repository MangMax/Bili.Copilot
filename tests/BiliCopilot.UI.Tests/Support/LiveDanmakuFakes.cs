// Copyright (c) Bili Copilot. All rights reserved.

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Tests.Support;

/// <summary>
/// 可控的直播弹幕服务：记录连接次数与房间号，按需返回假会话或抛异常。
/// </summary>
internal sealed class FakeLiveDanmakuService : ILiveDanmakuService
{
    private int _connectCount;

    public int ConnectCount => Volatile.Read(ref _connectCount);

    public string? LastRoomId { get; private set; }

    public ILiveDanmakuSession? Session { get; set; }

    public Func<string, CancellationToken, Task<ILiveDanmakuSession>>? ConnectOverride { get; set; }

    public Task<ILiveDanmakuSession> ConnectAsync(string roomId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _connectCount);
        LastRoomId = roomId;
        if (ConnectOverride is not null)
        {
            return ConnectOverride(roomId, cancellationToken);
        }

        var session = Session ?? new FakeLiveDanmakuSession(roomId);
        return Task.FromResult(session);
    }
}

/// <summary>
/// 假弹幕会话：事件由测试写入，停止/释放可观测。
/// </summary>
internal sealed class FakeLiveDanmakuSession : ILiveDanmakuSession
{
    private readonly Channel<LiveDanmakuEvent> _events = Channel.CreateUnbounded<LiveDanmakuEvent>();
    private int _stopCount;
    private int _disposeCount;

    public FakeLiveDanmakuSession(string roomId) => RoomId = roomId;

    public string RoomId { get; }

    public int StopCount => Volatile.Read(ref _stopCount);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public LiveDanmakuStatistics Statistics { get; set; } = new();

    public Task Completion => _events.Reader.Completion;

    public async IAsyncEnumerable<LiveDanmakuEvent> GetEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var liveEvent in _events.Reader.ReadAllAsync(cancellationToken))
        {
            yield return liveEvent;
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _stopCount);
        _events.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCount);
        _events.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    public void Emit(LiveDanmakuEvent liveEvent) => _events.Writer.TryWrite(liveEvent);

    public void Complete() => _events.Writer.TryComplete();
}

/// <summary>
/// 洪峰会话：在事件流里生成指定数量的弹幕，不占用测试进程的内存通道。
/// </summary>
internal sealed class FloodingLiveDanmakuSession : ILiveDanmakuSession
{
    private readonly int _count;

    public FloodingLiveDanmakuSession(string roomId, int count)
    {
        RoomId = roomId;
        _count = count;
    }

    public string RoomId { get; }

    public LiveDanmakuStatistics Statistics { get; } = new();

    public Task Completion { get; } = Task.CompletedTask;

    public async IAsyncEnumerable<LiveDanmakuEvent> GetEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < _count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return TestLiveEvents.Danmaku($"m{i}");
            if (i % 1024 == 1023)
            {
                await Task.Yield();
            }
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// 永久挂起的会话：故意忽略取消，用来验证停止是有界的。
/// </summary>
internal sealed class StuckLiveDanmakuSession : ILiveDanmakuSession
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string RoomId => "stuck";

    public LiveDanmakuStatistics Statistics { get; } = new();

    public Task Completion => _release.Task;

    public async IAsyncEnumerable<LiveDanmakuEvent> GetEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _release.Task;
        yield break;
    }

    public void Release() => _release.TrySetResult();

    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal static class TestLiveEvents
{
    public static LiveDanmakuReceivedEvent Danmaku(string text, int guardLevel = 0, bool isAdmin = false, string userName = "user")
        => new(
            new LiveDanmakuInformation(text, "#FFFFFF", userName, "10", "#FFFFFF", isAdmin)
            {
                GuardLevel = guardLevel,
                UserId = 42,
            },
            "DANMU_MSG",
            DateTimeOffset.UtcNow);

    public static LiveSuperChatEvent SuperChat(string message, int price = 30)
        => new(
            new LiveSuperChatInformation(
                Guid.NewGuid().ToString("N"),
                message,
                price,
                "payer",
                7,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(1),
                "#123456"),
            "SUPER_CHAT_MESSAGE",
            DateTimeOffset.UtcNow);

    public static LiveConnectionStateChangedEvent ConnectionState(LiveDanmakuConnectionState state, string? detail = null)
        => new(state, detail, DateTimeOffset.UtcNow);
}

internal static class TestWait
{
    public static async Task UntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("条件在超时前未满足。");
            }

            await Task.Delay(10);
        }
    }
}
