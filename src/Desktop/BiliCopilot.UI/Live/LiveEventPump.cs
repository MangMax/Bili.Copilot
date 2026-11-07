// Copyright (c) Bili Copilot. All rights reserved.

using System.Threading.Channels;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 一次直播观看的弹幕事件泵：建立唯一一条内核连接，把强类型事件有界地转交给上屏层，
/// 并把连接状态作为系统信号暴露给连接器。
/// </summary>
/// <remarks>
/// 一个实例只服务一次观看（<see cref="StartAsync"/> 只能成功调用一次）；关闭或换房时
/// 由连接器创建新的实例。事件先写入有界通道（满时丢最旧），注入层按固定节拍批量消费，
/// 因此不存在"每条消息一次 UI 调度"。
/// <see cref="EventReceived"/> 在事件泵线程触发，订阅者（#197 聊天、#198 房间状态）必须自行切回 UI 线程，
/// 且不得抛出异常；订阅者异常不会中断连接。
/// 停止是有界的：先取消枚举，再以 <c>stopTimeout</c> 等待泵任务与会话停止。
/// </remarks>
public sealed class LiveEventPump : IAsyncDisposable
{
    /// <summary>
    /// 默认事件缓冲容量。
    /// </summary>
    public const int DefaultEventCapacity = 2048;

    private static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(2);

    private readonly ILiveDanmakuService _service;
    private readonly Channel<LiveDanmakuEvent> _events;
    private readonly int _eventCapacity;
    private readonly TimeSpan _stopTimeout;
    private readonly object _gate = new();

    private CancellationTokenSource? _source;
    private ILiveDanmakuSession? _session;
    private Task? _pumpTask;
    private int _started;
    private volatile bool _stopped;
    private long _droppedEvents;
    private volatile LiveDanmakuConnectionState _state = LiveDanmakuConnectionState.Closed;
    private volatile string? _stateDetail;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveEventPump"/> class.
    /// </summary>
    /// <param name="service">内核直播弹幕服务.</param>
    /// <param name="eventCapacity">事件缓冲容量.</param>
    /// <param name="stopTimeout">停止时的有界等待.</param>
    public LiveEventPump(ILiveDanmakuService service, int eventCapacity = DefaultEventCapacity, TimeSpan? stopTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (eventCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(eventCapacity), "事件缓冲容量必须大于零。");
        }

        _service = service;
        _eventCapacity = eventCapacity;
        _stopTimeout = stopTimeout ?? DefaultStopTimeout;
        _events = Channel.CreateBounded<LiveDanmakuEvent>(new BoundedChannelOptions(eventCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true,
        });
    }

    /// <summary>
    /// Gets 给上屏层消费的有界事件通道.
    /// </summary>
    public ChannelReader<LiveDanmakuEvent> Events => _events.Reader;

    /// <summary>
    /// Gets 当前连接状态.
    /// </summary>
    public LiveDanmakuConnectionState State => _state;

    /// <summary>
    /// Gets 最近一次连接状态说明.
    /// </summary>
    public string? StateDetail => _stateDetail;

    /// <summary>
    /// Gets 内核返回的真实房间号；尚未连接时为空.
    /// </summary>
    public string? RoomId => _session?.RoomId;

    /// <summary>
    /// Gets 因缓冲已满而丢弃的事件数（诊断用，近似值）.
    /// </summary>
    public long DroppedEvents => Interlocked.Read(ref _droppedEvents);

    /// <summary>
    /// Gets 最近一次订阅者抛出的异常；订阅者异常不会中断连接.
    /// </summary>
    public Exception? LastSubscriberError { get; private set; }

    /// <summary>
    /// Gets 最近一次停止/释放会话时的异常.
    /// </summary>
    public Exception? LastCleanupError { get; private set; }

    /// <summary>
    /// Gets 事件泵任务；会话自然结束时完成.
    /// </summary>
    public Task Completion => _pumpTask ?? Task.CompletedTask;

    /// <summary>
    /// 连接状态变化（连接中/已连接/重连中/已关闭）。
    /// </summary>
    public event EventHandler<LiveDanmakuConnectionState>? StateChanged;

    /// <summary>
    /// 收到任意强类型事件；在事件泵线程触发，#197/#198 可据此扩展。
    /// </summary>
    public event EventHandler<LiveDanmakuEvent>? EventReceived;

    /// <summary>
    /// 建立本观看会话的弹幕连接；认证完成后返回 true。
    /// </summary>
    /// <param name="roomId">直播间标识，支持短号.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>已连接时为 true；被取消或房间号为空时为 false.</returns>
    /// <exception cref="InvalidOperationException">同一个实例重复启动.</exception>
    public async Task<bool> StartAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("一次观看只能建立一条弹幕连接。");
        }

        SetState(LiveDanmakuConnectionState.Connecting);
        if (_stopped || string.IsNullOrWhiteSpace(roomId) || cancellationToken.IsCancellationRequested)
        {
            _events.Writer.TryComplete();
            SetState(LiveDanmakuConnectionState.Closed);
            return false;
        }

        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_gate)
        {
            _source = source;
        }

        ILiveDanmakuSession session;
        try
        {
            session = await _service.ConnectAsync(roomId, source.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            DetachSource(source);
            source.Dispose();
            _events.Writer.TryComplete();
            SetState(LiveDanmakuConnectionState.Closed);
            return false;
        }
        catch (Exception ex)
        {
            DetachSource(source);
            source.Dispose();
            _events.Writer.TryComplete();
            SetState(LiveDanmakuConnectionState.Closed, ex.Message);
            throw;
        }

        var accepted = false;
        lock (_gate)
        {
            if (!_stopped && ReferenceEquals(_source, source) && !source.IsCancellationRequested)
            {
                _session = session;
                _pumpTask = PumpAsync(session, source.Token);
                accepted = true;
            }
        }

        if (!accepted)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            source.Dispose();
            _events.Writer.TryComplete();
            SetState(LiveDanmakuConnectionState.Closed);
            return false;
        }

        SetState(LiveDanmakuConnectionState.Connected);
        return true;
    }

    /// <summary>
    /// 停止事件泵并释放会话；可重复调用。
    /// </summary>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>停止任务.</returns>
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _stopped = true;
        CancellationTokenSource? source;
        Task? completion;
        ILiveDanmakuSession? session;
        lock (_gate)
        {
            source = _source;
            completion = _pumpTask;
            session = _session;
            _source = null;
            _pumpTask = null;
            _session = null;
        }

        if (source is null && session is null)
        {
            _events.Writer.TryComplete();
            SetState(LiveDanmakuConnectionState.Closed);
            return;
        }

        if (source is not null)
        {
            await source.CancelAsync().ConfigureAwait(false);
        }

        if (completion is not null)
        {
            try
            {
                await completion.WaitAsync(_stopTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // 有界等待：会话没有响应取消时不再无限阻塞。
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (session is not null)
        {
            using var stopSource = new CancellationTokenSource(_stopTimeout);
            try
            {
                await session.StopAsync(stopSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 停止失败不应阻止释放。
                LastCleanupError = ex;
            }

            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 释放失败不应阻止播放器关闭。
                LastCleanupError = ex;
            }
        }

        source?.Dispose();
        _events.Writer.TryComplete();
        SetState(LiveDanmakuConnectionState.Closed);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => StopAsync();

    private async Task PumpAsync(ILiveDanmakuSession session, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var liveEvent in session.GetEventsAsync(cancellationToken).ConfigureAwait(false))
            {
                RaiseEvent(liveEvent);
                if (liveEvent is LiveConnectionStateChangedEvent stateChanged)
                {
                    SetState(stateChanged.State, stateChanged.Detail);
                }

                if (_events.Reader.Count >= _eventCapacity)
                {
                    Interlocked.Increment(ref _droppedEvents);
                }

                _events.Writer.TryWrite(liveEvent);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SetState(LiveDanmakuConnectionState.Closed, ex.Message);
        }
        finally
        {
            _events.Writer.TryComplete();
            SetState(LiveDanmakuConnectionState.Closed);
        }
    }

    private void DetachSource(CancellationTokenSource source)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_source, source))
            {
                _source = null;
            }
        }
    }

    private void SetState(LiveDanmakuConnectionState state, string? detail = null)
    {
        var changed = _state != state || !string.Equals(_stateDetail, detail, StringComparison.Ordinal);
        _state = state;
        _stateDetail = detail;
        if (!changed)
        {
            return;
        }

        try
        {
            StateChanged?.Invoke(this, state);
        }
        catch (Exception ex)
        {
            // 订阅者异常不得中断连接。
            LastSubscriberError = ex;
        }
    }

    private void RaiseEvent(LiveDanmakuEvent liveEvent)
    {
        try
        {
            EventReceived?.Invoke(this, liveEvent);
        }
        catch (Exception ex)
        {
            // 订阅者异常不得中断连接。
            LastSubscriberError = ex;
        }
    }
}
