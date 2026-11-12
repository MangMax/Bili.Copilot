// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using Serilog.Events;

namespace BiliCopilot.UI.Logging;

/// <summary>
/// 按应用日志级别过滤日志事件，同时放行标记过的 mpv 输出.
/// </summary>
internal static class AppLogLevelFilter
{
    /// <summary>
    /// 判断日志事件是否应被应用日志级别排除.
    /// </summary>
    /// <param name="logEvent">日志事件.</param>
    /// <param name="appLevel">当前应用日志级别.</param>
    /// <returns>应排除时返回 <c>true</c>.</returns>
    public static bool ShouldExclude(LogEvent logEvent, AppLogLevel appLevel)
        => !IsMpvOutput(logEvent)
            && logEvent.Level < AppLogLevelSwitch.ToSerilogLevel(appLevel);

    /// <summary>
    /// 判断日志事件是否为 mpv 的 <c>[MPV]</c> 输出.
    /// </summary>
    /// <param name="logEvent">日志事件.</param>
    /// <returns>是 mpv 输出时返回 <c>true</c>.</returns>
    public static bool IsMpvOutput(LogEvent logEvent)
        => logEvent.Properties.TryGetValue(MpvLogging.MarkerPropertyName, out var marker)
            && marker is ScalarValue { Value: true }
            && logEvent.MessageTemplate.Text.StartsWith(MpvLogging.MessagePrefix, StringComparison.Ordinal);
}
