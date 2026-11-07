// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 构建视频的合作投稿人列表.
/// </summary>
public static class CollaboratorListBuilder
{
    /// <summary>
    /// 按首次出现顺序整理合作投稿人，排除主投稿人、无名称条目与重复用户.
    /// </summary>
    /// <param name="primary">主投稿人，可为空.</param>
    /// <param name="collaborators">内核返回的合作者列表.</param>
    /// <returns>可直接展示的合作投稿人列表；没有有效条目时为空列表.</returns>
    public static IReadOnlyList<PublisherProfile> Build(UserProfile? primary, IEnumerable<PublisherProfile?>? collaborators)
    {
        var result = new List<PublisherProfile>();
        if (collaborators is null)
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var collaborator in collaborators)
        {
            var user = collaborator?.User;
            if (user is null || string.IsNullOrWhiteSpace(user.Name))
            {
                continue;
            }

            if (primary is not null && string.Equals(primary.Id, user.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (seen.Add(user.Id))
            {
                result.Add(collaborator!);
            }
        }

        return result;
    }
}
