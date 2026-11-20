// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 发布标签和 MSIX 包版本的解析.
/// </summary>
/// <remarks>
/// 版本以包身份版本（四段式）为准；发布 tag 可能带前导 v，也可能少写末段。
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
        return trimmed[0] is 'v' or 'V'
            ? trimmed[1..].Trim()
            : trimmed;
    }

    /// <summary>
    /// 将发布标签转换成可安装的四段式 MSIX 版本.
    /// </summary>
    public static Version? ParsePackageVersion(string? tag)
    {
        if (!Version.TryParse(Normalize(tag), out var parsed)
            || parsed.Major > ushort.MaxValue || parsed.Minor > ushort.MaxValue
            || parsed.Build > ushort.MaxValue || parsed.Revision > ushort.MaxValue)
        {
            return null;
        }

        return new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
    }
}
