// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Models.Constants;

/// <summary>
/// 应用日志级别.
/// </summary>
public enum AppLogLevel
{
    /// <summary>
    /// 仅记录错误.
    /// </summary>
    Error,

    /// <summary>
    /// 记录警告及以上.
    /// </summary>
    Warning,

    /// <summary>
    /// 记录信息及以上.
    /// </summary>
    Information,

    /// <summary>
    /// 记录调试及以上.
    /// </summary>
    Debug,
}
