// Copyright (c) Bili Copilot. All rights reserved.

using Serilog.Core;
using Serilog.Events;

namespace BiliCopilot.UI.Logging;

/// <summary>
/// 识别弹幕渲染器每次更新配置时写出的 Information 刷屏日志。
/// </summary>
internal static class DanmakuConfigurationLogFilter
{
    private const string DanmakuSourceContext = "Richasy.Danmaku.DanmakuRenderer";
    private const string MessagePrefix = "[Danmaku] DanmakuDirectXRender.cs(";
    private const string MessageSuffix = ")->UpdateConfiguration(): Configuration updated";

    /// <summary>
    /// 判断日志事件是否为弹幕配置刷屏日志。仅比较来源、级别与消息模板文本，不做消息渲染。
    /// </summary>
    public static bool ShouldExclude(LogEvent logEvent)
    {
        if (logEvent.Level != LogEventLevel.Information
            || !logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var sourceContext)
            || sourceContext is not ScalarValue { Value: string sourceContextValue }
            || !string.Equals(sourceContextValue, DanmakuSourceContext, StringComparison.Ordinal))
        {
            return false;
        }

        var template = logEvent.MessageTemplate.Text;
        return template.Length >= MessagePrefix.Length + MessageSuffix.Length
            && template.StartsWith(MessagePrefix, StringComparison.Ordinal)
            && template.EndsWith(MessageSuffix, StringComparison.Ordinal);
    }
}
