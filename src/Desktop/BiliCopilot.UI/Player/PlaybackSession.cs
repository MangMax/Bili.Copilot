// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Player;

/// <summary>
/// 一次播放会话的生命周期归属：创建后只登记一次，初始化失败完整回滚，释放只执行一次。
/// </summary>
/// <typeparam name="TSession">被托管的会话对象。</typeparam>
internal sealed class PlaybackSession<TSession>
    where TSession : class
{
    private readonly Func<TSession, Task> _release;
    private readonly Action<PlaybackSession<TSession>> _unregister;
    private Task _initializationTask = Task.CompletedTask;
    private TaskCompletionSource? _releaseCompletion;
    private Task? _closeTask;
    private int _pendingInitializations;
    private bool _isReady;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackSession{TSession}"/> class.
    /// </summary>
    /// <param name="create">创建会话对象，并把它与本会话绑定。</param>
    /// <param name="release">释放会话对象已创建的原生资源。</param>
    /// <param name="unregister">从会话登记处注销。</param>
    public PlaybackSession(
        Func<PlaybackSession<TSession>, TSession> create,
        Func<TSession, Task> release,
        Action<PlaybackSession<TSession>> unregister)
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(unregister);
        _release = release;
        _unregister = unregister;
        Session = create(this);
    }

    /// <summary>
    /// 被托管的会话对象。
    /// </summary>
    public TSession Session { get; }

    /// <summary>
    /// 会话是否已完成过一次初始化。
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// 会话是否已开始释放；此后的初始化一律拒绝。
    /// </summary>
    public bool IsReleased => _releaseCompletion is not null;

    /// <summary>
    /// 关闭是否已开始；此后的初始化一律拒绝。
    /// </summary>
    public bool IsClosing => _closeTask is not null;

    /// <summary>
    /// 唯一的初始化入口：首次初始化失败时回滚已创建的资源，关闭/释放会等全部初始化结束。
    /// </summary>
    /// <param name="initialize">创建原生资源并初始化会话。</param>
    /// <returns>本次初始化任务；释放路径另有汇合全部初始化的任务。</returns>
    public Task InitializeAsync(Func<Task> initialize)
    {
        ArgumentNullException.ThrowIfNull(initialize);
        if (IsReleased || IsClosing)
        {
            return Task.CompletedTask;
        }

        var previous = _initializationTask;
        var initialization = RunInitializationAsync(initialize);
        // 关闭/释放要等全部初始化结束；每个调用方只等自己这一次，避免被别人的初始化拖住。
        _initializationTask = previous.IsCompleted ? initialization : Task.WhenAll(previous, initialization);
        return initialization;
    }

    /// <summary>
    /// 关闭闸门：重复关闭加入同一个任务，关闭工作只执行一次。
    /// </summary>
    /// <param name="close">关闭播放窗口的工作。</param>
    /// <returns>关闭任务。</returns>
    public Task CloseAsync(Func<Task> close)
    {
        ArgumentNullException.ThrowIfNull(close);
        if (_closeTask is not null)
        {
            return _closeTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _closeTask = completion.Task;
        _ = RunCloseAsync(close, completion);
        return _closeTask;
    }

    /// <summary>
    /// 释放闸门：重复释放加入同一个任务，原生资源只释放一次。
    /// </summary>
    /// <returns>释放任务。</returns>
    public Task ReleaseAsync() => EnsureReleaseAsync(awaitInitialization: true);

    private async Task RunInitializationAsync(Func<Task> initialize)
    {
        _pendingInitializations++;
        try
        {
            await initialize();
            _isReady = true;
        }
        catch
        {
            // 首次初始化失败时不会留下可用的播放器；此时完整回滚并注销，
            // 而不是把一个半初始化的会话留在登记处。
            // 另一个初始化仍在创建资源时不能拆掉它，二次加载失败也保留已就绪的播放器。
            if (!_isReady && _pendingInitializations == 1)
            {
                await RollbackAsync();
            }

            throw;
        }
        finally
        {
            _pendingInitializations--;
        }
    }

    private async Task RollbackAsync()
    {
        try
        {
            // 初始化任务就是当前任务，不能再等它自己。
            await EnsureReleaseAsync(awaitInitialization: false);
        }
        catch
        {
            // 回滚中的清理失败不应覆盖调用方要看到的初始化异常。
        }
    }

    private Task EnsureReleaseAsync(bool awaitInitialization)
    {
        if (_releaseCompletion is not null)
        {
            return _releaseCompletion.Task;
        }

        _releaseCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = RunReleaseAsync(awaitInitialization);
        return _releaseCompletion.Task;
    }

    private async Task RunReleaseAsync(bool awaitInitialization)
    {
        try
        {
            if (awaitInitialization)
            {
                // 有序退出：初始化还在创建原生资源时不能先销毁它们。
                try
                {
                    await _initializationTask;
                }
                catch
                {
                    // 初始化失败由它自己的调用方观察，这里只保证退出顺序。
                }
            }

            await _release(Session);
            _releaseCompletion!.SetResult();
        }
        catch (Exception ex)
        {
            _releaseCompletion!.SetException(ex);
        }
        finally
        {
            _unregister(this);
        }
    }

    private async Task RunCloseAsync(Func<Task> close, TaskCompletionSource completion)
    {
        try
        {
            await close();
            completion.SetResult();
        }
        catch (Exception ex)
        {
            completion.SetException(ex);
        }
    }
}

/// <summary>
/// 播放会话工厂：创建与登记只有这一个入口。
/// </summary>
/// <typeparam name="TSession">被托管的会话对象。</typeparam>
internal sealed class PlaybackSessionFactory<TSession>
    where TSession : class
{
    private readonly Func<PlaybackSession<TSession>, TSession> _create;
    private readonly Func<TSession, Task> _release;
    private readonly Action<TSession> _register;
    private readonly Action<TSession> _unregister;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackSessionFactory{TSession}"/> class.
    /// </summary>
    public PlaybackSessionFactory(
        Func<PlaybackSession<TSession>, TSession> create,
        Func<TSession, Task> release,
        Action<TSession> register,
        Action<TSession> unregister)
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(unregister);
        _create = create;
        _release = release;
        _register = register;
        _unregister = unregister;
    }

    /// <summary>
    /// 创建并登记一个会话；创建失败时不会登记。
    /// </summary>
    /// <returns>新会话。</returns>
    public PlaybackSession<TSession> Open()
    {
        var session = new PlaybackSession<TSession>(_create, _release, Unregister);
        _register(session.Session);
        return session;
    }

    private void Unregister(PlaybackSession<TSession> session) => _unregister(session.Session);
}
