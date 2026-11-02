// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.Resolvers;

/// <summary>
/// 清晰度候选：只携带选择所需的画质与权益标记。
/// </summary>
/// <param name="Quality">画质标识（B 站 DASH 的 quality 值，如 120 / 116 / 80）。</param>
/// <param name="IsEnabled">当前账号是否有权播放该画质（会员/大会员）。</param>
internal readonly record struct QualityCandidate(int Quality, bool IsEnabled);

/// <summary>
/// 权益快照：决定未解锁的清晰度是否参与选择。
/// </summary>
/// <param name="CanUseLockedFormats">UP 主本人可以选到未解锁清晰度；其他人只能选已解锁的。</param>
internal readonly record struct QualityEntitlement(bool CanUseLockedFormats)
{
    /// <summary>
    /// 普通观众：只能选当前账号已解锁的清晰度。
    /// </summary>
    public static QualityEntitlement Standard => new(false);

    /// <summary>
    /// 视频作者本人：未解锁清晰度也可参与选择。
    /// </summary>
    public static QualityEntitlement VideoAuthor => new(true);
}

/// <summary>
/// 选择结果的来源，用于 UI 展示与排查“偏好不可用”。
/// </summary>
internal enum QualitySelectionReason
{
    /// <summary>命中 <c>MediaSnapshot.PreferQuality</c> 指定的画质。</summary>
    PreferredQuality,

    /// <summary>“自动”偏好命中上次播放的清晰度。</summary>
    LastSelectedQuality,

    /// <summary>“4K 优先”命中 120。</summary>
    PreferUhd,

    /// <summary>“1080P 优先”命中 116 / 80。</summary>
    PreferHd,

    /// <summary>偏好不可用，回退到最高可用画质。</summary>
    FallbackHighest,

    /// <summary>没有提供任何清晰度。</summary>
    NoFormats,

    /// <summary>提供了清晰度，但权益过滤后没有可选项。</summary>
    NoEligibleFormats,
}

/// <summary>
/// 清晰度选择结果：不可用时 <see cref="Selected"/> 为空，原因说明为什么不可用。
/// </summary>
/// <param name="Selected">选中的清晰度；无可用项时为空。</param>
/// <param name="Reason">选择依据，或不可用的原因。</param>
internal readonly record struct QualitySelection(QualityCandidate? Selected, QualitySelectionReason Reason)
{
    /// <summary>
    /// 是否有选中项。
    /// </summary>
    public bool HasSelection => Selected.HasValue;

    /// <summary>
    /// 选中画质的 quality 值；无选中项时为空。
    /// </summary>
    public int? Quality => Selected?.Quality;
}

/// <summary>
/// 清晰度选择的纯输入：格式、权益快照、偏好，均与 WinUI / 账号 VM 无关。
/// </summary>
/// <param name="Formats">可用的清晰度（顺序即接口返回顺序，重复项取先出现的）。</param>
/// <param name="Entitlement">权益快照。</param>
/// <param name="PreferredQuality">本次播放指定的画质；为空或 0 表示未指定。</param>
/// <param name="Setting">清晰度偏好设置。</param>
/// <param name="LastSelectedQuality">上次播放的清晰度，0 表示没有记录。</param>
internal sealed record QualitySelectionRequest(
    IReadOnlyList<QualityCandidate> Formats,
    QualityEntitlement Entitlement,
    int? PreferredQuality,
    PreferQualityType Setting,
    int LastSelectedQuality);

/// <summary>
/// 清晰度选择的纯策略：输入格式与偏好，输出选中项和原因。
/// </summary>
/// <remarks>
/// 规则（与抽取前 <c>VideoMediaSourceResolver.GetCurrentFormat</c> 一致）：
/// 权益过滤 → 指定画质 → 偏好（自动/4K/1080P）→ 最高可用画质。
/// 空列表与权益过滤后为空不再抛异常，而是返回无选中项和原因。
/// </remarks>
internal static class QualitySelectionStrategy
{
    private const int UhdQuality = 120;
    private const int HdQuality = 116;
    private const int HdLegacyQuality = 80;

    /// <summary>
    /// 按权益与偏好选出清晰度。
    /// </summary>
    /// <param name="request">选择输入。</param>
    /// <returns>选中项及原因。</returns>
    public static QualitySelection Select(QualitySelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var formats = request.Formats;
        if (formats is null || formats.Count == 0)
        {
            return new QualitySelection(null, QualitySelectionReason.NoFormats);
        }

        var eligible = new List<QualityCandidate>(formats.Count);
        foreach (var format in formats)
        {
            if (request.Entitlement.CanUseLockedFormats || format.IsEnabled)
            {
                eligible.Add(format);
            }
        }

        if (eligible.Count == 0)
        {
            return new QualitySelection(null, QualitySelectionReason.NoEligibleFormats);
        }

        if (request.PreferredQuality is int preferredQuality && preferredQuality > 0)
        {
            var preferredIndex = IndexOf(eligible, preferredQuality);
            if (preferredIndex >= 0)
            {
                return new QualitySelection(eligible[preferredIndex], QualitySelectionReason.PreferredQuality);
            }
        }

        switch (request.Setting)
        {
            case PreferQualityType.Auto:
                var lastSelectedIndex = IndexOf(eligible, request.LastSelectedQuality);
                if (lastSelectedIndex >= 0)
                {
                    return new QualitySelection(eligible[lastSelectedIndex], QualitySelectionReason.LastSelectedQuality);
                }

                break;
            case PreferQualityType.UHD:
                var uhdIndex = IndexOf(eligible, UhdQuality);
                if (uhdIndex >= 0)
                {
                    return new QualitySelection(eligible[uhdIndex], QualitySelectionReason.PreferUhd);
                }

                break;
            case PreferQualityType.HD:
                var hdIndex = IndexOfPreferredHd(eligible);
                if (hdIndex >= 0)
                {
                    return new QualitySelection(eligible[hdIndex], QualitySelectionReason.PreferHd);
                }

                break;
            case PreferQualityType.High:
                // “最高画质”不需要额外筛选：与回退规则一样取最高可用画质。
                break;
            default:
                // 新增的偏好值默认按“最高可用画质”处理，避免语义静默改变。
                break;
        }

        var highestIndex = 0;
        for (var i = 1; i < eligible.Count; i++)
        {
            if (eligible[i].Quality > eligible[highestIndex].Quality)
            {
                highestIndex = i;
            }
        }

        return new QualitySelection(eligible[highestIndex], QualitySelectionReason.FallbackHighest);
    }

    private static int IndexOf(List<QualityCandidate> candidates, int quality)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Quality == quality)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfPreferredHd(List<QualityCandidate> candidates)
    {
        var bestIndex = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            var quality = candidates[i].Quality;
            if (quality != HdQuality && quality != HdLegacyQuality)
            {
                continue;
            }

            if (bestIndex < 0 || quality > candidates[bestIndex].Quality)
            {
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
