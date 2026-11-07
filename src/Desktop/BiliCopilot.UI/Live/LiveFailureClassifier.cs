// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播播放失败的分类。
/// </summary>
public enum LiveFailureKind
{
    /// <summary>
    /// 没有失败.
    /// </summary>
    None,

    /// <summary>
    /// 请求被取消（关闭窗口或换房）.
    /// </summary>
    Cancelled,

    /// <summary>
    /// 播放地址不可用（房间不存在、下播、断流、网络错误）.
    /// </summary>
    AddressUnavailable,

    /// <summary>
    /// 鉴权失败（登录态无效或没有该清晰度的权限）.
    /// </summary>
    AuthenticationFailed,

    /// <summary>
    /// 解码/格式失败.
    /// </summary>
    DecodeFailed,

    /// <summary>
    /// 其他未分类失败.
    /// </summary>
    Unknown,
}

/// <summary>
/// 把取流阶段抛出的异常翻译成可读的失败分类。
/// </summary>
/// <remarks>
/// 解码失败来自播放器内核的错误事件，不走这里；这里只区分请求地址与鉴权。
/// </remarks>
public static class LiveFailureClassifier
{
    /// <summary>
    /// 判定失败分类。
    /// </summary>
    /// <param name="exception">取流或初始化时抛出的异常.</param>
    /// <returns>失败分类.</returns>
    public static LiveFailureKind Classify(Exception? exception)
        => exception switch
        {
            null => LiveFailureKind.None,
            OperationCanceledException => LiveFailureKind.Cancelled,
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.PreconditionFailed }
                => LiveFailureKind.AuthenticationFailed,
            HttpRequestException => LiveFailureKind.AddressUnavailable,
            System.Security.Authentication.AuthenticationException => LiveFailureKind.AuthenticationFailed,
            IOException or System.Net.Sockets.SocketException => LiveFailureKind.AddressUnavailable,
            _ => LiveFailureKind.Unknown,
        };
}
