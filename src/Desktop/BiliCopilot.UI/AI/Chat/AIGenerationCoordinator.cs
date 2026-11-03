// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.AI.Chat;

/// <summary>
/// 生成代次协调器：每次请求开始时创建新代次，并取消旧代次.
/// </summary>
/// <remarks>
/// 协调器只保留最近一次请求.旧代次被取消后 <see cref="AIGeneration.IsCurrent"/> 为
/// <c>false</c>，其迟到分片与最终结果不会写入新代次.
/// </remarks>
internal sealed class AIGenerationCoordinator
{
    private readonly object _gate = new();
    private AIGeneration? _current;

    /// <summary>
    /// 开始新代次，并取消此前仍在进行的代次.
    /// </summary>
    /// <returns>新代次，调用方负责释放.</returns>
    public AIGeneration Begin()
    {
        var generation = new AIGeneration(this);
        AIGeneration? previous;
        lock (_gate)
        {
            previous = _current;
            _current = generation;
        }

        // 先登记新代次再取消旧代次：旧代次的取消回调即使同步执行，也不会影响新代次。
        previous?.Cancel();
        return generation;
    }

    /// <summary>
    /// 判断指定代次是否仍为当前代次.
    /// </summary>
    /// <param name="generation">待判定的代次.</param>
    /// <returns>是当前代次时返回 <c>true</c>.</returns>
    public bool IsCurrent(AIGeneration generation)
    {
        lock (_gate)
        {
            return ReferenceEquals(_current, generation);
        }
    }

    /// <summary>
    /// 在代次取消或释放后移除当前代次登记.
    /// </summary>
    /// <param name="generation">已结束的代次.</param>
    internal void Forget(AIGeneration generation)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_current, generation))
            {
                _current = null;
            }
        }
    }
}
