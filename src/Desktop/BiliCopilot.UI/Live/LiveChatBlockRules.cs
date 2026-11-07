// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.Frozen;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 屏蔽命中的维度.
/// </summary>
public enum LiveChatBlockKind
{
    /// <summary>
    /// 未命中任何规则.
    /// </summary>
    None,

    /// <summary>
    /// 命中用户 ID 规则.
    /// </summary>
    UserId,

    /// <summary>
    /// 命中昵称规则.
    /// </summary>
    UserName,

    /// <summary>
    /// 命中关键词规则.
    /// </summary>
    Keyword,
}

/// <summary>
/// 屏蔽计数快照.
/// </summary>
/// <param name="ByUserId">按用户 ID 屏蔽的条数.</param>
/// <param name="ByUserName">按昵称屏蔽的条数.</param>
/// <param name="ByKeyword">按关键词屏蔽的条数.</param>
public readonly record struct LiveChatBlockCounters(int ByUserId, int ByUserName, int ByKeyword)
{
    /// <summary>
    /// Gets 被屏蔽的总条数.
    /// </summary>
    public int Total => ByUserId + ByUserName + ByKeyword;
}

/// <summary>
/// 直播聊天的屏蔽规则：用户 ID、昵称、关键词三个维度，命中即不进聊天列表。
/// </summary>
/// <remarks>
/// 纯逻辑，不依赖 WinUI：热路径只做集合查找与子串匹配，规则集合用冻结集合避免每消息分配。
/// 计数在命中时累加，供界面显示"已屏蔽 N 条"。
/// 只做展示侧过滤，不涉及发送、礼物或撤回。
/// </remarks>
public sealed class LiveChatBlockRules
{
    private readonly object _gate = new();
    private FrozenSet<long> _userIds = FrozenSet<long>.Empty;
    private FrozenSet<string> _userNames = FrozenSet<string>.Empty;
    private string[] _keywords = [];
    private int _byUserId;
    private int _byUserName;
    private int _byKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatBlockRules"/> class.
    /// </summary>
    /// <param name="userIds">要屏蔽的用户 ID.</param>
    /// <param name="userNames">要屏蔽的昵称，忽略大小写与首尾空白.</param>
    /// <param name="keywords">要屏蔽的关键词，忽略大小写的子串匹配.</param>
    public LiveChatBlockRules(
        IEnumerable<long>? userIds = null,
        IEnumerable<string>? userNames = null,
        IEnumerable<string>? keywords = null)
    {
        Replace(userIds, userNames, keywords);
    }

    /// <summary>
    /// Gets a value indicating whether 当前没有任何屏蔽规则.
    /// </summary>
    public bool IsEmpty
        => Volatile.Read(ref _userIds).Count == 0
            && Volatile.Read(ref _userNames).Count == 0
            && Volatile.Read(ref _keywords).Length == 0;

    /// <summary>
    /// 整体替换规则；已有计数保留。
    /// </summary>
    /// <param name="userIds">要屏蔽的用户 ID.</param>
    /// <param name="userNames">要屏蔽的昵称.</param>
    /// <param name="keywords">要屏蔽的关键词.</param>
    public void Replace(
        IEnumerable<long>? userIds,
        IEnumerable<string>? userNames,
        IEnumerable<string>? keywords)
    {
        var ids = userIds?.Where(p => p > 0).ToFrozenSet() ?? FrozenSet<long>.Empty;
        var names = userNames is null
            ? FrozenSet<string>.Empty
            : userNames
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        var words = keywords is null
            ? []
            : keywords
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        lock (_gate)
        {
            _userIds = ids;
            _userNames = names;
            _keywords = words;
        }
    }

    /// <summary>
    /// 判断一条消息是否应被屏蔽；命中时累加对应计数。
    /// </summary>
    /// <param name="userId">发送者用户 ID.</param>
    /// <param name="userName">发送者昵称.</param>
    /// <param name="text">消息文本.</param>
    /// <returns>命中的屏蔽维度；未命中为 <see cref="LiveChatBlockKind.None"/>.</returns>
    public LiveChatBlockKind Evaluate(long userId, string? userName, string? text)
    {
        var kind = Match(userId, userName, text);
        switch (kind)
        {
            case LiveChatBlockKind.UserId:
                Interlocked.Increment(ref _byUserId);
                break;
            case LiveChatBlockKind.UserName:
                Interlocked.Increment(ref _byUserName);
                break;
            case LiveChatBlockKind.Keyword:
                Interlocked.Increment(ref _byKeyword);
                break;
            default:
                break;
        }

        return kind;
    }

    /// <summary>
    /// 读取计数快照。
    /// </summary>
    /// <returns>计数快照.</returns>
    public LiveChatBlockCounters Snapshot()
        => new(
            Volatile.Read(ref _byUserId),
            Volatile.Read(ref _byUserName),
            Volatile.Read(ref _byKeyword));

    private LiveChatBlockKind Match(long userId, string? userName, string? text)
    {
        var ids = Volatile.Read(ref _userIds);
        var names = Volatile.Read(ref _userNames);
        var words = Volatile.Read(ref _keywords);

        if (ids.Count == 0 && names.Count == 0 && words.Length == 0)
        {
            return LiveChatBlockKind.None;
        }

        if (userId > 0 && ids.Contains(userId))
        {
            return LiveChatBlockKind.UserId;
        }

        if (!string.IsNullOrWhiteSpace(userName) && names.Contains(userName.Trim()))
        {
            return LiveChatBlockKind.UserName;
        }

        if (!string.IsNullOrEmpty(text))
        {
            foreach (var word in words)
            {
                if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    return LiveChatBlockKind.Keyword;
                }
            }
        }

        return LiveChatBlockKind.None;
    }
}
