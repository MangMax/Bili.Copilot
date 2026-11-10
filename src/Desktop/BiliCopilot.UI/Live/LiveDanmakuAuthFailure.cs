// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Bili.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 弹幕认证失败的识别与文案提取（#197 要求聊天里出现"认证失败"系统消息）。
/// </summary>
/// <remarks>
/// 认证失败不重试，因此必须成为一条明确的系统提示，而不是静默断开。
/// 这里只做判定，不读取连接状态也不触发重连，便于直接用异常对象测试。
/// </remarks>
public static class LiveDanmakuAuthFailure
{
    /// <summary>
    /// 判断异常是否为弹幕认证失败。
    /// </summary>
    /// <param name="exception">连接/启动阶段抛出的异常.</param>
    /// <returns>认证失败时为 true.</returns>
    public static bool IsAuthenticationFailure(Exception? exception)
        => exception is LiveDanmakuUnauthorizedException;

    /// <summary>
    /// 提取可展示的失败说明。
    /// </summary>
    /// <param name="exception">连接/启动阶段抛出的异常.</param>
    /// <returns>失败说明；无法判定时为空.</returns>
    public static string? GetDetail(Exception? exception)
        => IsAuthenticationFailure(exception) ? exception!.Message : null;

    /// <summary>
    /// 判断连接关闭时的说明是否代表认证失败（服务端回执文本）。
    /// </summary>
    /// <param name="detail">连接状态附带的说明.</param>
    /// <returns>说明指向认证失败时为 true.</returns>
    public static bool IsAuthenticationDetail(string? detail)
        => !string.IsNullOrWhiteSpace(detail)
            && detail.Contains("认证", StringComparison.Ordinal);
}
