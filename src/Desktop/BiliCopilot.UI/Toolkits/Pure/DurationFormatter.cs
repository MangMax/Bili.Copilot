// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 时长格式化的纯函数，规则为满 1 小时用 hh:mm:ss，否则用 mm:ss.
/// </summary>
/// <remarks>
/// 放在 Toolkits/Pure 供界面工具组与 AI 上下文格式化共用，避免同一规则出现两份实现.
/// </remarks>
internal static class DurationFormatter
{
    /// <summary>
    /// 格式化时长.
    /// </summary>
    /// <param name="duration">待格式化的时长.</param>
    /// <returns>时长文本，如 01:33；满 1 小时时为 01:33:20.</returns>
    public static string Format(TimeSpan duration)
        => duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
}
