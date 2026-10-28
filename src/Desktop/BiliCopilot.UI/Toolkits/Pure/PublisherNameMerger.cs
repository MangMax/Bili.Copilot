// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 合并视频主投稿人与合作投稿人的名称.
/// </summary>
public static class PublisherNameMerger
{
    private const char Separator = '、';

    /// <summary>
    /// 按首次出现顺序合并投稿人名称，去除空白、忽略空值并按序数区分大小写去重.
    /// </summary>
    /// <param name="primary">主投稿人名称.</param>
    /// <param name="collaborators">合作投稿人名称.</param>
    /// <returns>以中文顿号分隔的投稿人名称；没有有效名称时返回空字符串.</returns>
    public static string Merge(string? primary, IEnumerable<string?>? collaborators)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddName(primary, names, seen);

        if (collaborators is not null)
        {
            foreach (var name in collaborators)
            {
                AddName(name, names, seen);
            }
        }

        return string.Join(Separator, names);
    }

    private static void AddName(string? name, List<string> names, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var trimmed = name.Trim();
        if (seen.Add(trimmed))
        {
            names.Add(trimmed);
        }
    }
}
