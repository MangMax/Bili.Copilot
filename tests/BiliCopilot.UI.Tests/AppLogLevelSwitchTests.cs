// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Logging;
using BiliCopilot.UI.Models.Constants;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace BiliCopilot.UI.Tests;

public class AppLogLevelSwitchTests
{
    [Fact]
    public void Apply_ChangesWhichEventsReachTheSinkAtRuntime()
    {
        var sink = new CollectingSink();
        var levelSwitch = new AppLogLevelSwitch(AppLogLevel.Information);
        using var logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch.LevelSwitch)
            .Filter.ByExcluding(logEvent => AppLogLevelFilter.ShouldExclude(logEvent, levelSwitch.Current))
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("information before");
        logger.Debug("debug before");

        Assert.Equal(["information before"], sink.Messages);

        levelSwitch.Apply(AppLogLevel.Debug);
        logger.Debug("debug after");

        Assert.Contains("debug after", sink.Messages);

        levelSwitch.Apply(AppLogLevel.Error);
        logger.Information("information after");
        logger.Error("error after");

        Assert.DoesNotContain("information after", sink.Messages);
        Assert.Contains("error after", sink.Messages);
    }

    [Theory]
    [InlineData(AppLogLevel.Error, LogEventLevel.Error)]
    [InlineData(AppLogLevel.Warning, LogEventLevel.Warning)]
    [InlineData(AppLogLevel.Information, LogEventLevel.Information)]
    [InlineData(AppLogLevel.Debug, LogEventLevel.Debug)]
    public void ToSerilogLevel_MapsTheSelectedLevel(AppLogLevel appLevel, LogEventLevel expected)
    {
        Assert.Equal(expected, AppLogLevelSwitch.ToSerilogLevel(appLevel));
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<string> Messages { get; } = [];

        public void Emit(LogEvent logEvent) => Messages.Add(logEvent.RenderMessage());
    }
}
