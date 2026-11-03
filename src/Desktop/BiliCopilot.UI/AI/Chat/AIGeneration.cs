// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 一次 AI 生成请求的代次，持有取消令牌并判定自身是否仍是当前请求.
/// </summary>
/// <remarks>
/// 代次由 <see cref="AIGenerationCoordinator"/> 创建.新请求开始或显式取消后，
/// <see cref="IsCurrent"/> 变为 <c>false</c>，用于阻止旧代次的迟到回调继续发布内容.
/// </remarks>
internal sealed class AIGeneration : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly AIGenerationCoordinator _coordinator;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIGeneration"/> class.
    /// </summary>
    /// <param name="coordinator">负责判定当前代次的协调器.</param>
    internal AIGeneration(AIGenerationCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        _coordinator = coordinator;
    }

    /// <summary>
    /// 获取该代次的取消令牌.
    /// </summary>
    public CancellationToken Token => _cancellation.Token;

    /// <summary>
    /// 获取该代次是否仍是协调器中唯一的当前代次且尚未取消.
    /// </summary>
    public bool IsCurrent
        => !_disposed && !_cancellation.IsCancellationRequested && _coordinator.IsCurrent(this);

    /// <summary>
    /// 取消该代次，触发已注册的取消回调.
    /// </summary>
    public void Cancel()
    {
        if (_disposed || _cancellation.IsCancellationRequested)
        {
            return;
        }

        _cancellation.Cancel();
    }

    /// <summary>
    /// 释放取消令牌资源；重复调用安全.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _coordinator.Forget(this);
        _cancellation.Dispose();
    }
}
