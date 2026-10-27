// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手跳过判定器.
/// <para>
/// 负责把播放进度与片段列表做匹配，并记录已经跳过的片段，
/// 避免跳转后的进度事件再次触发同一片段而造成跳回或循环.
/// </para>
/// </summary>
public sealed class SponsorBlockSkipTracker
{
    /// <summary>
    /// 开始时间容差（秒），用于吸收进度事件的时间抖动.
    /// </summary>
    public const double StartToleranceSeconds = 0.5;

    /// <summary>
    /// 片段剩余时长低于该值（秒）时不再跳转，避免无意义的原地跳转.
    /// </summary>
    public const double MinimumRemainingSeconds = 0.5;

    /// <summary>
    /// 两次进度检查之间的最小间隔.
    /// </summary>
    public static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromMilliseconds(250);

    private readonly HashSet<string> _skippedSegmentKeys = new(StringComparer.Ordinal);
    private DateTimeOffset _lastCheckAt;
    private bool _hasChecked;

    /// <summary>
    /// 判断当前位置是否落在某个未跳过的片段内；若是，则记录该片段并返回跳转目标.
    /// </summary>
    /// <param name="segments">当前分 P 的片段列表（通常已按开始时间排序）.</param>
    /// <param name="position">当前播放位置（秒）.</param>
    /// <param name="duration">视频总时长（秒），未知时传 0.</param>
    /// <param name="now">当前时间，用于节流.</param>
    /// <param name="segment">命中的片段.</param>
    /// <param name="targetPosition">建议跳转到的位置（秒）.</param>
    /// <returns>需要跳转时返回 <c>true</c>.</returns>
    public bool TryTakeSkip(
        IReadOnlyList<SponsorBlockSegment> segments,
        double position,
        double duration,
        DateTimeOffset now,
        out SponsorBlockSegment? segment,
        out double targetPosition)
    {
        segment = null;
        targetPosition = 0;

        if (segments is null || segments.Count == 0
            || !IsFinite(position) || position < 0)
        {
            return false;
        }

        if (_hasChecked && now - _lastCheckAt < MinimumCheckInterval)
        {
            return false;
        }

        _hasChecked = true;
        _lastCheckAt = now;

        var effectiveDuration = IsFinite(duration) && duration > 0
            ? duration
            : double.PositiveInfinity;

        foreach (var candidate in segments)
        {
            if (candidate is null
                || !SponsorBlockSegment.IsValidRange(candidate.Start, candidate.End)
                || position < candidate.Start - StartToleranceSeconds
                || position >= candidate.End)
            {
                continue;
            }

            var key = CreateKey(candidate);
            if (_skippedSegmentKeys.Contains(key))
            {
                continue;
            }

            var target = Math.Min(candidate.End, effectiveDuration);
            if (target - position < MinimumRemainingSeconds)
            {
                continue;
            }

            _ = _skippedSegmentKeys.Add(key);
            segment = candidate;
            targetPosition = target;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 重置跳过历史与节流状态，切换视频或分 P 时调用.
    /// </summary>
    public void Reset()
    {
        _skippedSegmentKeys.Clear();
        _hasChecked = false;
        _lastCheckAt = default;
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private static string CreateKey(SponsorBlockSegment segment)
        => string.IsNullOrWhiteSpace(segment.Uuid)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{segment.Category}:{segment.Start:F3}:{segment.End:F3}")
            : segment.Uuid!;
}
