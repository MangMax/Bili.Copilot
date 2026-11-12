// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Logging;
using Serilog;
using Serilog.Core;
using static BiliCopilot.UI.Tests.DanmakuConfigurationLogFilterTestData;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 用真实 Serilog 文件承载验证过滤后的落盘体积与其它日志的保留情况。
/// </summary>
public class DanmakuConfigurationLogFilterSinkTests
{
    private const int NoiseEventCount = 100_000;
    private const long SizeThreshold = 100 * 1024;

    [Fact]
    public void FileSink_DropsDanmakuConfigurationNoise_AndKeepsOtherEvents()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BiliCopilot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "danmaku-filter.log");

        try
        {
            using (var logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Filter.ByExcluding(DanmakuConfigurationLogFilter.ShouldExclude)
                .WriteTo.File(path, outputTemplate: OutputTemplate)
                .CreateLogger())
            {
                WriteNoiseBatch(logger);

                logger.Information("playback started");
                logger.Warning("playback warning");
                logger.Error("playback error");
            }

            var length = new FileInfo(path).Length;
            Assert.True(length < SizeThreshold, $"Filtered log grew to {length} bytes.");

            var content = File.ReadAllText(path);
            Assert.Contains("playback started", content);
            Assert.Contains("playback warning", content);
            Assert.Contains("playback error", content);
            Assert.DoesNotContain("UpdateConfiguration", content);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileSink_UnfilteredControl_EmitsAllDanmakuNoiseEvents()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BiliCopilot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "danmaku-control.log");

        try
        {
            using (var logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(path, outputTemplate: OutputTemplate)
                .CreateLogger())
            {
                WriteNoiseBatch(logger);
            }

            var length = new FileInfo(path).Length;
            Assert.True(length > SizeThreshold, $"Control log only grew to {length} bytes.");
            Assert.Contains("UpdateConfiguration", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void WriteNoiseBatch(ILogger logger)
    {
        var danmakuLogger = logger.ForContext(Constants.SourceContextPropertyName, DanmakuSourceContext);
        for (var i = 0; i < NoiseEventCount; i++)
        {
            danmakuLogger.Information(NoiseTemplate);
        }
    }
}
