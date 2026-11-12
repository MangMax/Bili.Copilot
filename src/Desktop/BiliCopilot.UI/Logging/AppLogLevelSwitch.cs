// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using Serilog.Core;
using Serilog.Events;

namespace BiliCopilot.UI.Logging;

/// <summary>
/// 可运行时切换的应用日志级别.
/// </summary>
public sealed class AppLogLevelSwitch
{
    private readonly LoggingLevelSwitch _levelSwitch;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppLogLevelSwitch"/> class.
    /// </summary>
    /// <param name="initialLevel">初始日志级别.</param>
    public AppLogLevelSwitch(AppLogLevel initialLevel)
    {
        Current = initialLevel;
        _levelSwitch = new LoggingLevelSwitch(ToEffectiveMinimum(initialLevel));
    }

    /// <summary>
    /// 应用全局使用的日志级别开关.
    /// </summary>
    public static AppLogLevelSwitch Default { get; } = new(AppLogLevel.Information);

    /// <summary>
    /// 当前应用日志级别.
    /// </summary>
    public AppLogLevel Current { get; private set; }

    /// <summary>
    /// 交给 Serilog 的级别开关.
    /// </summary>
    internal LoggingLevelSwitch LevelSwitch => _levelSwitch;

    /// <summary>
    /// 立即应用新的应用日志级别.
    /// </summary>
    /// <param name="level">新的日志级别.</param>
    public void Apply(AppLogLevel level)
    {
        Current = level;
        _levelSwitch.MinimumLevel = ToEffectiveMinimum(level);
    }

    /// <summary>
    /// 计算 Serilog 管道允许通过的最低级别：始终放行 Information 及以上，供逐事件过滤区分 mpv 与普通应用日志.
    /// </summary>
    /// <param name="level">应用日志级别.</param>
    /// <returns>Serilog 管道最低级别.</returns>
    internal static LogEventLevel ToEffectiveMinimum(AppLogLevel level)
    {
        var mapped = ToSerilogLevel(level);
        return mapped < LogEventLevel.Information ? mapped : LogEventLevel.Information;
    }

    /// <summary>
    /// 将应用日志级别映射为 Serilog 级别.
    /// </summary>
    /// <param name="level">应用日志级别.</param>
    /// <returns>对应的 Serilog 级别.</returns>
    public static LogEventLevel ToSerilogLevel(AppLogLevel level) => level switch
    {
        AppLogLevel.Error => LogEventLevel.Error,
        AppLogLevel.Warning => LogEventLevel.Warning,
        AppLogLevel.Information => LogEventLevel.Information,
        AppLogLevel.Debug => LogEventLevel.Debug,
        _ => LogEventLevel.Information,
    };
}
