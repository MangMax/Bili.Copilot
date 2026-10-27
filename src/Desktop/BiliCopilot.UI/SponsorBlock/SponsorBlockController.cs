// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 空降助手行为控制器.
/// <para>
/// 根据片段类别配置决定自动跳过、静音、显示按钮或不处理，并维护已处理片段、
/// 静音窗口与提示的生命周期。该类不依赖 WinUI，可直接进行单元测试.
/// </para>
/// </summary>
public sealed class SponsorBlockController
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

    /// <summary>
    /// 跳过提示的默认显示时长.
    /// </summary>
    public static readonly TimeSpan PromptTimeout = TimeSpan.FromSeconds(8);

    private readonly Func<SponsorBlockCategory, SponsorBlockBehavior> _behaviorResolver;
    private readonly TimeSpan _promptTimeout;
    private readonly HashSet<string> _handledSegments = new(StringComparer.Ordinal);
    private SponsorBlockSegment? _activeMuteSegment;
    private SponsorBlockSegment? _activePromptSegment;
    private DateTimeOffset _promptShownAt;
    private bool _muteApplied;
    private bool _muteSuppressed;
    private bool _muteRestoreState;
    private bool _hasChecked;
    private DateTimeOffset _lastCheckAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockController"/> class.
    /// </summary>
    /// <param name="behaviorResolver">按类别读取行为配置的委托，为空时使用默认行为.</param>
    /// <param name="promptTimeout">跳过提示的显示时长，为空时使用 <see cref="PromptTimeout"/>.</param>
    public SponsorBlockController(
        Func<SponsorBlockCategory, SponsorBlockBehavior>? behaviorResolver = null,
        TimeSpan? promptTimeout = null)
    {
        _behaviorResolver = behaviorResolver ?? SponsorBlockBehaviorDefaults.GetDefault;
        _promptTimeout = promptTimeout ?? PromptTimeout;
        if (_promptTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(promptTimeout));
        }
    }

    /// <summary>
    /// 是否由控制器静音了当前片段.
    /// </summary>
    public bool IsMuteApplied => _muteApplied;

    /// <summary>
    /// 是否正在显示跳过提示.
    /// </summary>
    public bool IsPromptActive => _activePromptSegment is not null;

    /// <summary>
    /// 根据当前播放位置计算需要执行的动作.
    /// </summary>
    /// <param name="segments">当前分 P 的片段列表（通常已按开始时间排序）.</param>
    /// <param name="position">当前播放位置（秒）.</param>
    /// <param name="duration">视频总时长（秒），未知时传 0.</param>
    /// <param name="isMuted">播放器当前是否静音，用于在进入静音片段时记录用户原本的状态.</param>
    /// <param name="now">当前时间，用于节流与提示超时判断.</param>
    /// <returns>需要执行的决策；无需处理时返回 <see cref="SponsorBlockActionKind.None"/>.</returns>
    public SponsorBlockDecision Evaluate(
        IReadOnlyList<SponsorBlockSegment>? segments,
        double position,
        double duration,
        bool isMuted,
        DateTimeOffset now)
    {
        if (segments is null || segments.Count == 0
            || !IsFinite(position) || position < 0)
        {
            return default;
        }

        if (_hasChecked && now - _lastCheckAt < MinimumCheckInterval)
        {
            return default;
        }

        _hasChecked = true;
        _lastCheckAt = now;

        var effectiveDuration = IsFinite(duration) && duration > 0
            ? duration
            : double.PositiveInfinity;
        var segmentAtPosition = FindSegmentAt(segments, position);

        if (_activePromptSegment is not null)
        {
            if (position >= _activePromptSegment.End
                || position < _activePromptSegment.Start - StartToleranceSeconds
                || now - _promptShownAt >= _promptTimeout)
            {
                return CompletePrompt(_activePromptSegment);
            }

            return default;
        }

        if (_activeMuteSegment is not null)
        {
            var leftMuteSegment = position >= _activeMuteSegment.End
                || position < _activeMuteSegment.Start - StartToleranceSeconds;
            if (!leftMuteSegment)
            {
                if (!_muteSuppressed && !_muteApplied)
                {
                    _muteApplied = true;
                    _muteRestoreState = isMuted;
                    return new SponsorBlockDecision(
                        SponsorBlockActionKind.Mute,
                        _activeMuteSegment,
                        0,
                        true);
                }

                return default;
            }

            var restoreState = _muteApplied ? _muteRestoreState : (bool?)null;
            var muteSegment = _activeMuteSegment;
            ClearMute();
            if (restoreState.HasValue)
            {
                return new SponsorBlockDecision(
                    SponsorBlockActionKind.RestoreMute,
                    muteSegment,
                    0,
                    restoreState.Value);
            }
        }

        if (segmentAtPosition is null)
        {
            return default;
        }

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
            if (_handledSegments.Contains(key))
            {
                continue;
            }

            switch (_behaviorResolver(candidate.Category))
            {
                case SponsorBlockBehavior.Skip:
                    {
                        var target = Math.Min(candidate.End, effectiveDuration);
                        if (target - position < MinimumRemainingSeconds)
                        {
                            continue;
                        }

                        _ = _handledSegments.Add(key);
                        return new SponsorBlockDecision(
                            SponsorBlockActionKind.Skip,
                            candidate,
                            target);
                    }

                case SponsorBlockBehavior.Mute:
                    {
                        _activeMuteSegment = candidate;
                        _muteApplied = true;
                        _muteSuppressed = false;
                        _muteRestoreState = isMuted;
                        return new SponsorBlockDecision(
                            SponsorBlockActionKind.Mute,
                            candidate,
                            0,
                            true);
                    }

                case SponsorBlockBehavior.ShowButton:
                    {
                        _activePromptSegment = candidate;
                        _promptShownAt = now;
                        return new SponsorBlockDecision(
                            SponsorBlockActionKind.ShowButton,
                            candidate);
                    }

                default:
                    continue;
            }
        }

        return default;
    }

    /// <summary>
    /// 用户点击跳过提示：跳转到片段终点，并确保该片段不再提示.
    /// </summary>
    /// <param name="duration">视频总时长（秒），未知时传 0.</param>
    /// <returns>跳转决策；没有活动提示时返回 <see cref="SponsorBlockActionKind.None"/>.</returns>
    public SponsorBlockDecision AcceptActivePrompt(double duration)
    {
        var segment = _activePromptSegment;
        if (segment is null)
        {
            return default;
        }

        ClearPrompt(segment);
        var target = IsFinite(duration) && duration > 0
            ? Math.Min(segment.End, duration)
            : segment.End;
        return new SponsorBlockDecision(SponsorBlockActionKind.Skip, segment, target);
    }

    /// <summary>
    /// 用户关闭或提示超时：隐藏提示，并确保该片段不再提示.
    /// </summary>
    /// <returns>隐藏提示的决策；没有活动提示时返回 <see cref="SponsorBlockActionKind.None"/>.</returns>
    public SponsorBlockDecision DismissActivePrompt()
    {
        var segment = _activePromptSegment;
        if (segment is null)
        {
            return default;
        }

        ClearPrompt(segment);
        return new SponsorBlockDecision(SponsorBlockActionKind.HideButton, segment);
    }

    /// <summary>
    /// 通知控制器用户手动切换了静音状态。当前静音片段剩余时间内不再接管静音，
    /// 离开片段时也不会覆盖用户的选择.
    /// </summary>
    public void NotifyUserMuteChanged()
    {
        if (_activeMuteSegment is null)
        {
            return;
        }

        _muteApplied = false;
        _muteSuppressed = true;
    }

    /// <summary>
    /// 重置全部状态，切换视频或分 P 时调用.
    /// </summary>
    /// <returns>
    /// 若控制器正在静音，返回恢复静音的决策；否则若有活动提示，返回隐藏提示的决策；
    /// 都没有时返回 <see cref="SponsorBlockActionKind.None"/>.
    /// </returns>
    public SponsorBlockDecision Reset()
    {
        var restoreState = _muteApplied ? _muteRestoreState : (bool?)null;
        var prompt = _activePromptSegment;

        _handledSegments.Clear();
        _activeMuteSegment = null;
        _activePromptSegment = null;
        _promptShownAt = default;
        _muteApplied = false;
        _muteSuppressed = false;
        _muteRestoreState = false;
        _hasChecked = false;
        _lastCheckAt = default;

        if (restoreState.HasValue)
        {
            return new SponsorBlockDecision(
                SponsorBlockActionKind.RestoreMute,
                null,
                0,
                restoreState.Value);
        }

        return prompt is null
            ? default
            : new SponsorBlockDecision(SponsorBlockActionKind.HideButton, prompt);
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private static SponsorBlockSegment? FindSegmentAt(
        IReadOnlyList<SponsorBlockSegment> segments,
        double position)
    {
        foreach (var segment in segments)
        {
            if (segment is not null
                && SponsorBlockSegment.IsValidRange(segment.Start, segment.End)
                && position >= segment.Start - StartToleranceSeconds
                && position < segment.End)
            {
                return segment;
            }
        }

        return null;
    }

    private static string CreateKey(SponsorBlockSegment segment)
        => string.IsNullOrWhiteSpace(segment.Uuid)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{segment.Category}:{segment.Start:F3}:{segment.End:F3}")
            : segment.Uuid!;

    private SponsorBlockDecision CompletePrompt(SponsorBlockSegment segment)
    {
        ClearPrompt(segment);
        return new SponsorBlockDecision(SponsorBlockActionKind.HideButton, segment);
    }

    private void ClearPrompt(SponsorBlockSegment segment)
    {
        _ = _handledSegments.Add(CreateKey(segment));
        _activePromptSegment = null;
        _promptShownAt = default;
    }

    private void ClearMute()
    {
        _activeMuteSegment = null;
        _muteApplied = false;
        _muteSuppressed = false;
        _muteRestoreState = false;
    }
}
