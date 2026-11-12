// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Logging;
using BiliCopilot.UI.Models.Constants;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;

namespace BiliCopilot.UI.Tests;

public class MpvLogLevelIndependenceTests
{
    [Theory]
    [InlineData(AppLogLevel.Warning)]
    [InlineData(AppLogLevel.Error)]
    public void MarkedMpvInformation_BypassesAppLevel_WhileAppInformationDoesNot(AppLogLevel appLevel)
    {
        var sink = new CollectingSink();
        var levelSwitch = new AppLogLevelSwitch(appLevel);
        using var logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch.LevelSwitch)
            .Filter.ByExcluding(logEvent => AppLogLevelFilter.ShouldExclude(logEvent, levelSwitch.Current))
            .WriteTo.Sink(sink)
            .CreateLogger();
        using var factory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddSerilog(logger));
        var mpvLogger = MpvLogging.CreateLogger(logger);

        mpvLogger.LogInformation("[MPV] Event received: file-loaded");
        mpvLogger.LogDebug("[MPV] Debug event");
        factory.CreateLogger("BiliCopilot.UI.Tests.App").LogInformation("app information");

        Assert.Contains("[MPV] Event received: file-loaded", sink.Messages);
        Assert.DoesNotContain("[MPV] Debug event", sink.Messages);
        Assert.DoesNotContain("app information", sink.Messages);
    }

    [Fact]
    public void MarkedLoggerMessageWithoutMpvPrefix_RemainsSubjectToAppLevel()
    {
        var sink = new CollectingSink();
        var levelSwitch = new AppLogLevelSwitch(AppLogLevel.Warning);
        using var logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch.LevelSwitch)
            .Filter.ByExcluding(logEvent => AppLogLevelFilter.ShouldExclude(logEvent, levelSwitch.Current))
            .WriteTo.Sink(sink)
            .CreateLogger();
        var mpvLogger = MpvLogging.CreateLogger(logger);

        mpvLogger.LogInformation("Set Mpv log level to Warn");

        Assert.DoesNotContain("Set Mpv log level to Warn", sink.Messages);
    }

    [Fact]
    public void FileSink_AppLevelChangesAtRuntime_AndMpvInformationSurvivesErrorLevel()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BiliCopilot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "mpv-independence.log");

        try
        {
            var levelSwitch = new AppLogLevelSwitch(AppLogLevel.Information);
            using (var logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch.LevelSwitch)
                .Filter.ByExcluding(logEvent => AppLogLevelFilter.ShouldExclude(logEvent, levelSwitch.Current))
                .WriteTo.File(path)
                .CreateLogger())
            using (var factory = LoggerFactory.Create(builder => builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddSerilog(logger)))
            {
                var appLogger = factory.CreateLogger("BiliCopilot.UI.Tests.App");
                var mpvLogger = MpvLogging.CreateLogger(logger);

                appLogger.LogInformation("app information before change");
                mpvLogger.LogInformation("[MPV] Event received: before change");

                levelSwitch.Apply(AppLogLevel.Error);

                appLogger.LogInformation("app information after change");
                appLogger.LogError("app error after change");
                mpvLogger.LogInformation("[MPV] Event received: after change");
            }

            var content = File.ReadAllText(path);
            Assert.Contains("app information before change", content);
            Assert.DoesNotContain("app information after change", content);
            Assert.Contains("app error after change", content);
            Assert.Contains("[MPV] Event received: before change", content);
            Assert.Contains("[MPV] Event received: after change", content);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<string> Messages { get; } = [];

        public void Emit(Serilog.Events.LogEvent logEvent) => Messages.Add(logEvent.RenderMessage());
    }
}
