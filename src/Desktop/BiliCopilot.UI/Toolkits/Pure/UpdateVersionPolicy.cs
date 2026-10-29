// Copyright (c) Bili Copilot. All rights reserved.

using System.Globalization;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 更新版本的归一与比较.
/// </summary>
/// <remarks>
/// 版本以包身份版本（四段式）为准；发布 tag 可能带前导 v，也可能少写末段。
/// 比较按数值逐段进行，缺位补 0，不做字符串比较。
/// </remarks>
public static class UpdateVersionPolicy
{
    /// <summary>
    /// 去掉空白与可选的前导 v/V.
    /// </summary>
    /// <param name="version">原始版本或 tag.</param>
    /// <returns>归一后的版本文本；输入为空时返回空字符串.</returns>
    public static string Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var trimmed = version.Trim();
        return trimmed.Length > 0 && (trimmed[0] == 'v' || trimmed[0] == 'V')
            ? trimmed[1..].Trim()
            : trimmed;
    }

    /// <summary>
    /// 把版本拆成数值段.
    /// </summary>
    /// <param name="version">原始版本或 tag.</param>
    /// <returns>各段的数值；无法识别出数字的段记为 0。</returns>
    public static IReadOnlyList<int> ParseParts(string? version)
    {
        var normalized = Normalize(version);
        if (normalized.Length == 0)
        {
            return [];
        }

        var parts = new List<int>();
        foreach (var segment in normalized.Split('.'))
        {
            var digits = new string([.. segment.TakeWhile(char.IsDigit)]);
            parts.Add(digits.Length == 0 ? 0 : int.Parse(digits, CultureInfo.InvariantCulture));
        }

        return parts;
    }

    /// <summary>
    /// 按数值逐段比较两个版本，缺位补 0.
    /// </summary>
    /// <param name="left">左侧版本.</param>
    /// <param name="right">右侧版本.</param>
    /// <returns>左侧小于/等于/大于右侧时分别返回负数/0/正数.</returns>
    public static int Compare(string? left, string? right)
    {
        var leftParts = ParseParts(left);
        var rightParts = ParseParts(right);
        var count = Math.Max(leftParts.Count, rightParts.Count);
        for (var i = 0; i < count; i++)
        {
            var leftPart = i < leftParts.Count ? leftParts[i] : 0;
            var rightPart = i < rightParts.Count ? rightParts[i] : 0;
            if (leftPart != rightPart)
            {
                return leftPart.CompareTo(rightPart);
            }
        }

        return 0;
    }

    /// <summary>
    /// 判断远端版本是否比本地新.
    /// </summary>
    /// <param name="localVersion">本地版本.</param>
    /// <param name="remoteVersion">远端版本.</param>
    /// <returns>远端严格更新时返回 true.</returns>
    public static bool IsRemoteNewer(string? localVersion, string? remoteVersion)
        => Compare(localVersion, remoteVersion) < 0;
}
