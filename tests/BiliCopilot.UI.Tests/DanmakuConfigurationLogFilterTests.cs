// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 弹幕配置刷屏日志的过滤规则：只丢弃渲染器每次重配时写出的 Information 行。
/// </summary>
public class DanmakuConfigurationLogFilterTests
{
    private const string DanmakuSourceContext = "Richasy.Danmaku.DanmakuRenderer";
    private const string NoiseTemplate = "[Danmaku] DanmakuDirectXRender.cs(43)->UpdateConfiguration(): Configuration updated";

    [Theory]
    [InlineData(43)]
    [InlineData(999)]
    public void ShouldExclude_DanmakuConfigurationInformation_IsExcluded(int lineNumber)
    {
        var template = $"[Danmaku] DanmakuDirectXRender.cs({lineNumber})->UpdateConfiguration(): Configuration updated";

        Assert.True(DanmakuConfigurationLogFilter.ShouldExclude(
            CreateEvent(LogEventLevel.Information, DanmakuSourceContext, template)));
    }

    [Fact]
    public void ShouldExclude_UnrelatedInformationContainingConfiguration_IsWritten()
    {
        Assert.False(DanmakuConfigurationLogFilter.ShouldExclude(
            CreateEvent(LogEventLevel.Information, "BiliCopilot.UI.Services.Settings", "[Settings] Configuration updated")));
    }

    [Fact]
    public void ShouldExclude_SameTextFromAnotherSourceContext_IsWritten()
    {
        Assert.False(DanmakuConfigurationLogFilter.ShouldExclude(
            CreateEvent(LogEventLevel.Information, "BiliCopilot.UI.Other", NoiseTemplate)));
    }

    [Theory]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    public void ShouldExclude_WarningOrErrorFromDanmakuSource_IsWritten(LogEventLevel level)
    {
        Assert.False(DanmakuConfigurationLogFilter.ShouldExclude(
            CreateEvent(level, DanmakuSourceContext, NoiseTemplate)));
    }

    [Fact]
    public void ShouldExclude_OtherInformationFromDanmakuSource_IsWritten()
    {
        Assert.False(DanmakuConfigurationLogFilter.ShouldExclude(
            CreateEvent(LogEventLevel.Information, DanmakuSourceContext, "[Danmaku] Renderer initialized")));
    }

    private static LogEvent CreateEvent(LogEventLevel level, string sourceContext, string template)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            exception: null,
            new MessageTemplateParser().Parse(template),
            [new LogEventProperty(Constants.SourceContextPropertyName, new ScalarValue(sourceContext))]);
    }
}
