// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Logging;
using Microsoft.Extensions.Logging;
using Serilog;
using static BiliCopilot.UI.Tests.DanmakuConfigurationLogFilterTestData;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 验证真实的 Microsoft.Extensions.Logging -> Serilog 管道会把日志类别写入 SourceContext，
/// 从而确保生产环境的过滤规则不是静默失效。
/// </summary>
public class DanmakuConfigurationLogFilterPipelineTests
{
    [Fact]
    public void LoggingPipeline_SetsDanmakuSourceContext_AndFilterDropsNoise()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BiliCopilot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "pipeline.log");

        try
        {
            using (var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Filter.ByExcluding(DanmakuConfigurationLogFilter.ShouldExclude)
                .WriteTo.File(path, outputTemplate: OutputTemplate)
                .CreateLogger())
            {
                using (var factory = LoggerFactory.Create(builder => builder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddSerilog(serilogLogger)))
                {
                    factory.CreateLogger(DanmakuSourceContext).LogInformation(NoiseTemplate);
                    factory.CreateLogger("BiliCopilot.UI.Tests.OtherCategory")
                        .LogInformation("ordinary information from another category");
                }
            }

            var content = File.ReadAllText(path);
            Assert.DoesNotContain("UpdateConfiguration", content);
            Assert.Contains("ordinary information from another category", content);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
