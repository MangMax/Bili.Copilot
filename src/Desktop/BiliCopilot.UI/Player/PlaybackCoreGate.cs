// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Player;

/// <summary>
/// 播放核心（mpv）生死闸门：<c>MpvClient</c> 的 Shutdown 触发后，
/// 该会话被标记为 dead，之后所有发往死句柄的命令一律在入口短路。
/// 会话死亡不可逆；重建（#228 的 DisposeAsync + InitializeAsync 路径）
/// 通过开启新实例/调用 <see cref="Reset"/> 恢复。
/// </summary>
internal sealed class PlaybackCoreGate
{
    private volatile bool _coreDead;

    /// <summary>
    /// 当前会话的播放核心是否已被 Shutdown 标记为 dead。
    /// </summary>
    public bool IsCoreDead => _coreDead;

    /// <summary>
    /// 命令入口的统一短路判断：核心已死或客户端已释放时返回 true，
    /// 调用方必须直接返回，不得再向 <c>MpvClient</c>/<c>MpvPlayer</c> 下发命令。
    /// </summary>
    /// <param name="clientDisposed">客户端的 <c>IsDisposed</c>；null 视为已释放。</param>
    /// <returns>应当短路时为 true。</returns>
    public bool ShouldShortCircuit(bool? clientDisposed)
        => _coreDead || clientDisposed != false;

    /// <summary>
    /// 由 <c>MpvClient.Shutdown</c> 调用：把该会话标记为 dead。
    /// 幂等，重复触发返回 false。
    /// </summary>
    /// <returns>首次标记返回 true；已经 dead 返回 false。</returns>
    public bool MarkDead()
    {
        if (_coreDead)
        {
            return false;
        }

        _coreDead = true;
        return true;
    }

    /// <summary>
    /// 重建播放核心后由新会话/新客户端复位：仅供 #228 的重建路径使用。
    /// </summary>
    public void Reset() => _coreDead = false;
}
