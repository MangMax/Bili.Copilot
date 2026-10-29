// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 用户附加加速源的解析与记忆过期判断.
/// </summary>
public static class UpdateMirrorPolicy
{
    /// <summary>
    /// 前缀允许的最大长度，避免设置值被滥用.
    /// </summary>
    public const int MaxPrefixLength = 200;

    /// <summary>
    /// 解析用户输入的加速源前缀，非法行被忽略.
    /// </summary>
    /// <param name="raw">每行一个前缀的文本.</param>
    /// <returns>合法的 https 前缀.</returns>
    public static IReadOnlyList<string> ParseUserPrefixes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var prefixes = new List<string>();
        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Length <= MaxPrefixLength
                && line.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && line.Length > "https://".Length)
            {
                prefixes.Add(line);
            }
        }

        return prefixes;
    }

    /// <summary>
    /// 判断记住的源是否仍在有效期内.
    /// </summary>
    /// <param name="recordedTicks">记录时间（UTC ticks）.</param>
    /// <param name="nowTicks">当前时间（UTC ticks）.</param>
    /// <param name="maxAgeTicks">有效期长度（ticks）.</param>
    /// <returns>记录有效时返回 true.</returns>
    public static bool IsSourceFresh(long recordedTicks, long nowTicks, long maxAgeTicks)
        => recordedTicks > 0
        && nowTicks > recordedTicks
        && nowTicks - recordedTicks <= maxAgeTicks;
}
