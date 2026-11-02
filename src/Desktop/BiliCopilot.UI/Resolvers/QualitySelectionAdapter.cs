// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.ViewModels.Items;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Resolvers;

/// <summary>
/// 把播放接口的清晰度与账号权益转换成 <see cref="QualitySelectionStrategy"/> 的纯输入。
/// </summary>
/// <remarks>
/// 与账号 VM 的耦合保留在这一层：<see cref="SourceItemViewModel.IsEnabled"/>
/// 由账号会员状态算出，策略本身不感知账号。
/// </remarks>
internal static class QualitySelectionAdapter
{
    /// <summary>
    /// 按权益与偏好选出清晰度。
    /// </summary>
    /// <param name="formats">播放接口返回的清晰度列表。</param>
    /// <param name="entitlement">权益快照。</param>
    /// <param name="preferredQuality">本次播放指定的画质；为空或 0 表示未指定。</param>
    /// <param name="setting">清晰度偏好设置。</param>
    /// <param name="lastSelectedQuality">上次播放的清晰度，0 表示没有记录。</param>
    /// <returns>选中的清晰度；没有可用项时为空。</returns>
    public static PlayerFormatInformation? SelectFormat(
        IList<PlayerFormatInformation>? formats,
        QualityEntitlement entitlement,
        int? preferredQuality,
        PreferQualityType setting,
        int lastSelectedQuality)
    {
        if (formats is null || formats.Count == 0)
        {
            return null;
        }

        var candidates = new List<QualityCandidate>(formats.Count);
        foreach (var format in formats)
        {
            var item = new SourceItemViewModel(format, default);
            candidates.Add(new QualityCandidate(format.Quality, item.IsEnabled));
        }

        var selection = QualitySelectionStrategy.Select(
            new QualitySelectionRequest(candidates, entitlement, preferredQuality, setting, lastSelectedQuality));

        if (selection.Selected is not { } selected)
        {
            return null;
        }

        foreach (var format in formats)
        {
            if (format.Quality == selected.Quality)
            {
                return format;
            }
        }

        return null;
    }
}
