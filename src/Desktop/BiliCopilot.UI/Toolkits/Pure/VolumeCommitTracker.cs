// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 音量落盘缓冲：合并连续变更，只保留等待提交的最后一次音量。
/// </summary>
/// <remarks>
/// 播放器音量在拖动或按键时会连续变化，落盘只关心最后一次的值。
/// 这里只负责保存与取出待提交值，计时与写设置在 UI 层完成，以便在无 WinUI 依赖的测试项目中验证。
/// </remarks>
internal sealed class VolumeCommitTracker
{
    private double? _pendingVolume;

    /// <summary>
    /// 是否有等待提交的音量。
    /// </summary>
    public bool HasPending => _pendingVolume.HasValue;

    /// <summary>
    /// 记录一次音量变更，后记录的值覆盖先前的值。
    /// </summary>
    /// <param name="volume">最新的播放器音量。</param>
    public void Record(double volume)
        => _pendingVolume = volume;

    /// <summary>
    /// 取出并清除等待提交的音量。
    /// </summary>
    /// <param name="volume">待提交的音量。</param>
    /// <returns>存在待提交音量时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    public bool TryTakePending(out double volume)
    {
        if (_pendingVolume is not double pending)
        {
            volume = default;
            return false;
        }

        _pendingVolume = null;
        volume = pending;
        return true;
    }
}
