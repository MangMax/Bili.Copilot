// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;
using System.Text.RegularExpressions;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 账号资料加载失败的类别.
/// </summary>
public enum AccountProfileLoadFailureKind
{
    /// <summary>
    /// 网络或服务暂时不可用，可以重试，不应清理登录态.
    /// </summary>
    Transient,

    /// <summary>
    /// 登录凭据已失效，需要重新登录.
    /// </summary>
    AuthenticationExpired,
}

/// <summary>
/// 判断账号资料加载失败属于网络故障还是登录态失效.
/// </summary>
/// <remarks>
/// 内核把 HTTP 失败、空响应与业务错误码都包成同一种异常，调用方拿不到区分信息。
/// 这里沿异常链逐层检查状态码与错误文本，只有确证登录态失效才让用户重新登录；
/// 认不出来的失败一律按可重试处理，宁可多给一次重试，也不能误清登录态。
/// </remarks>
public static class AccountProfileLoadFailureClassifier
{
    /// <summary>
    /// 异常链的最大展开层数，防止异常链成环时无限展开.
    /// </summary>
    private const int MaxChainDepth = 32;

    /// <summary>
    /// 表示登录态已失效的错误文本，命中任意一条即可判定需要重新登录.
    /// </summary>
    private static readonly string[] AuthenticationMarkers =
    [
        "请重新登录",
        "未登录",
        "登录已失效",
    ];

    /// <summary>
    /// 哔哩哔哩业务响应体里的错误码，-101 表示账号未登录，-111 表示 csrf 校验失败.
    /// </summary>
    private static readonly Regex ApiCodePattern = new("\"code\"\\s*:\\s*(-?\\d+)", RegexOptions.CultureInvariant);

    /// <summary>
    /// 对一次资料加载失败进行分类.
    /// </summary>
    /// <param name="exception">加载失败抛出的异常.</param>
    /// <returns>失败类别；无法识别时返回 <see cref="AccountProfileLoadFailureKind.Transient"/>.</returns>
    public static AccountProfileLoadFailureKind Classify(Exception? exception)
    {
        foreach (var node in EnumerateChain(exception))
        {
            if (IsAuthenticationFailure(node))
            {
                return AccountProfileLoadFailureKind.AuthenticationExpired;
            }
        }

        return AccountProfileLoadFailureKind.Transient;
    }

    private static bool IsAuthenticationFailure(Exception node)
        => HasUnauthorizedStatus(node) || HasAuthenticationMarker(node) || HasAuthenticationApiCode(node);

    private static bool HasUnauthorizedStatus(Exception node)
        => node is HttpRequestException httpException
            && httpException.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    private static bool HasAuthenticationMarker(Exception node)
        => AuthenticationMarkers.Any(marker => node.Message.Contains(marker, StringComparison.Ordinal));

    private static bool HasAuthenticationApiCode(Exception node)
    {
        var match = ApiCodePattern.Match(node.Message);
        return match.Success
            && match.Groups[1].Value is "-101" or "-111";
    }

    /// <summary>
    /// 按广度优先展开异常链，包括 <see cref="AggregateException"/> 的并行分支.
    /// </summary>
    /// <param name="exception">根异常.</param>
    /// <returns>去重后的异常节点，最多 <see cref="MaxChainDepth"/> 层.</returns>
    private static IEnumerable<Exception> EnumerateChain(Exception? exception)
    {
        if (exception is null)
        {
            yield break;
        }

        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<(Exception Exception, int Depth)>();
        pending.Enqueue((exception, 0));

        while (pending.Count > 0)
        {
            var (current, depth) = pending.Dequeue();
            if (depth >= MaxChainDepth || !visited.Add(current))
            {
                continue;
            }

            yield return current;

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    pending.Enqueue((inner, depth + 1));
                }
            }
            else if (current.InnerException is { } innerException)
            {
                pending.Enqueue((innerException, depth + 1));
            }
        }
    }
}
