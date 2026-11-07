// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Services.Live;

/// <summary>
/// 直播弹幕内核连接的最小应用侧消费点：把强类型事件计数写入日志，用于验证事件真实到达.
/// </summary>
public sealed class LiveDanmakuProbeService : IAsyncDisposable
{
    private static readonly TimeSpan LogInterval = TimeSpan.FromSeconds(10);

    private readonly ILiveDanmakuService _liveDanmakuService;
    private readonly ILogger<LiveDanmakuProbeService> _logger;
    private readonly object _gate = new();

    private ILiveDanmakuSession? _session;
    private CancellationTokenSource? _source;
    private Task? _pump;
    private long _receivedEvents;
    private long _danmakuCount;
    private long _superChatCount;
    private long _onlineCountUpdates;
    private long _stateChanges;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDanmakuProbeService"/> class.
    /// </summary>
    public LiveDanmakuProbeService(ILiveDanmakuService liveDanmakuService, ILogger<LiveDanmakuProbeService> logger)
    {
        _liveDanmakuService = liveDanmakuService;
        _logger = logger;
    }

    /// <summary>
    /// 当前真实房间号.
    /// </summary>
    public string? RoomId => _session?.RoomId;

    /// <summary>
    /// 内核统计快照.
    /// </summary>
    public LiveDanmakuStatistics? Statistics => _session?.Statistics;

    /// <summary>
    /// 已收到的事件总数.
    /// </summary>
    public long ReceivedEvents => Interlocked.Read(ref _receivedEvents);

    /// <summary>
    /// 弹幕事件数.
    /// </summary>
    public long DanmakuCount => Interlocked.Read(ref _danmakuCount);

    /// <summary>
    /// 醒目留言事件数.
    /// </summary>
    public long SuperChatCount => Interlocked.Read(ref _superChatCount);

    /// <summary>
    /// 在线人数字更新数.
    /// </summary>
    public long OnlineCountUpdates => Interlocked.Read(ref _onlineCountUpdates);

    /// <summary>
    /// 连接状态变化数.
    /// </summary>
    public long StateChanges => Interlocked.Read(ref _stateChanges);

    /// <summary>
    /// 启动指定直播间的弹幕探测；重复调用会先停止旧会话.
    /// </summary>
    /// <param name="roomId">直播间标识.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>启动任务，完成时已认证.</returns>
    public async Task StartAsync(string roomId, CancellationToken cancellationToken = default)
    {
        await StopAsync().ConfigureAwait(false);
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ILiveDanmakuSession session;
        try
        {
            session = await _liveDanmakuService.ConnectAsync(roomId, source.Token).ConfigureAwait(false);
        }
        catch
        {
            source.Dispose();
            throw;
        }

        lock (_gate)
        {
            _session = session;
            _source = source;
            _pump = PumpAsync(session, source.Token);
        }
    }

    /// <summary>
    /// 停止探测并释放会话.
    /// </summary>
    /// <returns>停止任务.</returns>
    public async ValueTask StopAsync()
    {
        CancellationTokenSource? source;
        Task? pump;
        ILiveDanmakuSession? session;
        lock (_gate)
        {
            source = _source;
            pump = _pump;
            session = _session;
            _source = null;
            _pump = null;
            _session = null;
        }

        if (source is null)
        {
            return;
        }

        await source.CancelAsync().ConfigureAwait(false);
        if (pump is not null)
        {
            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        source.Dispose();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => StopAsync();

    private async Task PumpAsync(ILiveDanmakuSession session, CancellationToken cancellationToken)
    {
        var lastLog = DateTimeOffset.UtcNow;
        try
        {
            await foreach (var liveEvent in session.GetEventsAsync(cancellationToken).ConfigureAwait(false))
            {
                Interlocked.Increment(ref _receivedEvents);
                switch (liveEvent)
                {
                    case LiveDanmakuReceivedEvent:
                        Interlocked.Increment(ref _danmakuCount);
                        break;
                    case LiveSuperChatEvent or LiveSuperChatDeletedEvent:
                        Interlocked.Increment(ref _superChatCount);
                        break;
                    case LiveOnlineCountChangedEvent:
                        Interlocked.Increment(ref _onlineCountUpdates);
                        break;
                    case LiveConnectionStateChangedEvent:
                        Interlocked.Increment(ref _stateChanges);
                        break;
                }

                var now = DateTimeOffset.UtcNow;
                if (now - lastLog >= LogInterval)
                {
                    lastLog = now;
                    LogCounters("live danmaku probe progress");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live danmaku probe stopped unexpectedly for room {RoomId}.", session.RoomId);
        }
        finally
        {
            LogCounters("live danmaku probe stopped");
        }
    }

    private void LogCounters(string message)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var statistics = Statistics;
        _logger.LogInformation(
            "{Message}: room={RoomId} events={Events} danmaku={Danmaku} superChat={SuperChat} online={Online} state={State} unknown={Unknown} malformed={Malformed} reconnects={Reconnects}",
            message,
            RoomId,
            ReceivedEvents,
            DanmakuCount,
            SuperChatCount,
            OnlineCountUpdates,
            StateChanges,
            statistics?.UnknownCommands ?? 0,
            statistics?.MalformedMessages ?? 0,
            statistics?.Reconnects ?? 0);
    }
}