// Copyright (c) Bili Copilot. All rights reserved.

using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace BiliCopilot.UI.Logging;

/// <summary>
/// 从应用 Serilog 管道创建带 mpv 标记的日志记录器.
/// </summary>
internal static class MpvLogging
{
    /// <summary>
    /// mpv 日志事件的标记属性名称.
    /// </summary>
    public const string MarkerPropertyName = "BiliCopilot.MpvOutput";

    /// <summary>
    /// mpv 日志消息的统一前缀.
    /// </summary>
    public const string MessagePrefix = "[MPV]";

    private const string LoggerCategory = "Richasy.MpvKernel";

    /// <summary>
    /// 使用应用全局 Serilog 管道创建带 mpv 标记的日志记录器.
    /// </summary>
    /// <returns>日志记录器.</returns>
    public static ILogger CreateLogger() => CreateLogger(Log.Logger);

    /// <summary>
    /// 使用指定 Serilog 记录器创建带 mpv 标记的日志记录器.
    /// </summary>
    /// <param name="serilogLogger">应用的 Serilog 记录器.</param>
    /// <returns>日志记录器.</returns>
    public static ILogger CreateLogger(Serilog.ILogger serilogLogger)
    {
        ArgumentNullException.ThrowIfNull(serilogLogger);
        var markedLogger = serilogLogger.ForContext(MarkerPropertyName, true);
        var provider = new SerilogLoggerProvider(markedLogger);
        return provider.CreateLogger(LoggerCategory);
    }
}
