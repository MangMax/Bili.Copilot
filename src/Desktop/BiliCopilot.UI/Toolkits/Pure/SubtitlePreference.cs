// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 字幕语言偏好的纯逻辑。
/// </summary>
/// <remarks>
/// 规则：中文非 AI &gt; 中文 AI &gt; 其他语言非 AI &gt; 列表首个。
/// 排序与具体 UI 无关，便于在测试项目中验证。
/// </remarks>
internal static class SubtitlePreference
{
    private const int ChineseNonAIRank = 0;
    private const int ChineseAIRank = 1;
    private const int OtherNonAIRank = 2;
    private const int FallbackRank = 3;

    /// <summary>
    /// 按语言偏好选出字幕索引，空列表返回 -1。
    /// </summary>
    /// <param name="metas">字幕元数据，包含语言名称与是否为 AI 字幕。</param>
    /// <returns>首选字幕在列表中的索引；没有可用字幕时为 -1。</returns>
    public static int SelectPreferredIndex(IReadOnlyList<(string LanguageName, bool IsAI)> metas)
        => SelectPreferredIndex(metas, p => p.LanguageName, p => p.IsAI);

    /// <summary>
    /// 按语言偏好选出字幕索引，空列表返回 -1。
    /// </summary>
    /// <typeparam name="T">字幕元数据类型。</typeparam>
    /// <param name="metas">字幕元数据列表。</param>
    /// <param name="getLanguageName">读取字幕语言名称。</param>
    /// <param name="getIsAI">读取字幕是否为 AI 生成。</param>
    /// <returns>首选字幕在列表中的索引；没有可用字幕时为 -1。</returns>
    public static int SelectPreferredIndex<T>(IReadOnlyList<T> metas, Func<T, string?> getLanguageName, Func<T, bool> getIsAI)
    {
        ArgumentNullException.ThrowIfNull(getLanguageName);
        ArgumentNullException.ThrowIfNull(getIsAI);

        if (metas is null || metas.Count == 0)
        {
            return -1;
        }

        var bestIndex = 0;
        var bestRank = FallbackRank;
        for (var i = 0; i < metas.Count; i++)
        {
            var rank = GetRank(getLanguageName(metas[i]), getIsAI(metas[i]));
            if (rank < bestRank)
            {
                bestRank = rank;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int GetRank(string? languageName, bool isAI)
    {
        if (IsChinese(languageName))
        {
            return isAI ? ChineseAIRank : ChineseNonAIRank;
        }

        return isAI ? FallbackRank : OtherNonAIRank;
    }

    private static bool IsChinese(string? languageName)
    {
        if (string.IsNullOrWhiteSpace(languageName))
        {
            return false;
        }

        var name = languageName.Trim();
        return name.Contains("中文", StringComparison.Ordinal)
            || name.Contains("简体", StringComparison.Ordinal)
            || name.Contains("繁体", StringComparison.Ordinal)
            || name.Contains("汉语", StringComparison.Ordinal)
            || name.Contains("chinese", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }
}
