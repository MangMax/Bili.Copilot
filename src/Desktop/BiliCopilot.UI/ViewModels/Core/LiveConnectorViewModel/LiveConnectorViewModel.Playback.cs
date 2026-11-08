// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Resolvers;
using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播连接器的画质/线路选择（#194）：列表来自解析器当前的
/// <c>LiveMediaInformation.Formats</c> 与排序后的 <c>LiveLineInformation</c> 候选，
/// 选择后统一走播放器的刷新准入重新取址；偏好由 <see cref="ApplyPlaybackInfo"/> 在刷新后回填。
/// </summary>
public sealed partial class LiveConnectorViewModel
{
    private bool _isSyncingPlayback;

    /// <summary>
    /// 用解析器的最新播放信息回填画质与线路列表；刷新后调用，保证界面不回到默认值。
    /// </summary>
    /// <param name="resolver">直播媒体源解析器.</param>
    internal void ApplyPlaybackInfo(LiveMediaSourceResolver resolver)
    {
        var playback = resolver.PlaybackInfo;
        if (playback is null)
        {
            // 取址失败时保留上一次可读的画质/线路，不要把界面清空。
            return;
        }

        _isSyncingPlayback = true;
        try
        {
            Session.SetPlayback(playback);
            Qualities.Clear();
            // 内核会把同一清晰度的多种编码都列出来；界面按 qn 去重，只留一个可选项。
            var seenQualities = new HashSet<int>();
            foreach (var format in playback.Formats)
            {
                if (!seenQualities.Add(format.Quality))
                {
                    continue;
                }

                Qualities.Add(new LiveQualityOption(format.Quality, format.Description));
            }

            SelectedQuality = Qualities.FirstOrDefault(option => option.Quality == resolver.SelectedQuality)
                ?? Qualities.FirstOrDefault();

            Lines.Clear();
            // 候选是“格式 × 线路”的笛卡尔积；按原始主机名去重并排序，显示为“线路一/二/三”。
            var orderedLines = resolver.Candidates
                .Where(candidate => !string.IsNullOrEmpty(candidate.Primary.Host))
                .GroupBy(candidate => candidate.Primary.Host, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.Primary.Host, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var i = 0; i < orderedLines.Count; i++)
            {
                Lines.Add(new LiveLineOption(orderedLines[i].Key, $"线路{ToLineNumber(i + 1)}"));
            }

            // 用户刚选的线路优先于解析器当前仍在使用的候选，否则刷新后会被旧值拉回第一条。
            SelectedLine = Lines.FirstOrDefault(option => string.Equals(option.Key, resolver.PreferredLineKey, StringComparison.OrdinalIgnoreCase))
                ?? Lines.FirstOrDefault(option => string.Equals(option.Key, resolver.CurrentCandidate?.Key, StringComparison.OrdinalIgnoreCase))
                ?? Lines.FirstOrDefault();

            QualityText = SelectedQuality?.Name;
            LineText = SelectedLine?.Name;
        }
        finally
        {
            _isSyncingPlayback = false;
        }
    }

    partial void OnSelectedQualityChanged(LiveQualityOption? value)
    {
        if (_isSyncingPlayback || value is null)
        {
            return;
        }

        _ = Parent.ChangeLiveQualityAsync(value.Quality);
    }

    partial void OnSelectedLineChanged(LiveLineOption? value)
    {
        if (_isSyncingPlayback || value is null)
        {
            return;
        }

        _ = Parent.ChangeLiveLineAsync(value.Key);
    }

    private static string ToLineNumber(int index)
        => index switch
        {
            1 => "一",
            2 => "二",
            3 => "三",
            4 => "四",
            5 => "五",
            6 => "六",
            7 => "七",
            8 => "八",
            9 => "九",
            10 => "十",
            _ => index.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    private static string DescribeLine(LivePlaybackCandidate candidate)
    {
        var host = candidate.Primary.Host;
        var fallbackName = ResourceToolkit.GetLocalizedString(StringNames.LiveLineFallbackName);
        var label = string.IsNullOrEmpty(host)
            ? candidate.CodecName ?? fallbackName
            : string.IsNullOrEmpty(candidate.CodecName) ? host : candidate.CodecName + " · " + host;
        return candidate.IsMcdn
            ? label + ResourceToolkit.GetLocalizedString(StringNames.LiveLineMcdnSuffix)
            : label;
    }
}

/// <summary>
/// 画质选项；只持有清晰度标识与展示名，不持有任何地址.
/// </summary>
/// <param name="Quality">清晰度标识.</param>
/// <param name="Name">展示名.</param>
public sealed record LiveQualityOption(int Quality, string Name);

/// <summary>
/// 线路选项；Key 是候选首选地址的域名，刷新后仍能定位同一条线路.
/// </summary>
/// <param name="Key">稳定标识.</param>
/// <param name="Name">展示名.</param>
public sealed record LiveLineOption(string Key, string Name);