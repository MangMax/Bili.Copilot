using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 直播 mpv 配置（#199 结论）：必须有 ffmpeg 重连与 HLS 片级重试，
/// 不能有 reconnect_at_eof / 整体 low-latency profile。
/// </summary>
public sealed class LiveMpvConfigTests
{
    [Fact]
    public void Content_ContainsRequiredLiveOptions()
    {
        Assert.Contains("stream-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_delay_max=5", LiveMpvConfig.Content);
        Assert.Contains("demuxer-lavf-o=live_start_index=-1,seg_max_retry=3", LiveMpvConfig.Content);
        Assert.Contains("video-sync=audio", LiveMpvConfig.Content);
        Assert.Contains("framedrop=vo", LiveMpvConfig.Content);
    }

    [Fact]
    public void Content_DoesNotContainKnownHarmfulProfiles()
    {
        Assert.DoesNotContain("reconnect_at_eof", LiveMpvConfig.Content);
        Assert.DoesNotContain("profile=low-latency", LiveMpvConfig.Content);
        Assert.DoesNotContain("cache-pause=no", LiveMpvConfig.Content);
    }

    [Fact]
    public async Task EnsureFile_WritesLiveAndBaselineConfigsOnlyOnce()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bili-live-mpv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var live = await LiveMpvConfig.EnsureFileAsync(directory);
            var baseline = await LiveMpvConfig.EnsureFileAsync(directory, baseline: true);

            Assert.Equal(Path.Combine(directory, LiveMpvConfig.FileName), live);
            Assert.Equal(Path.Combine(directory, LiveMpvConfig.BaselineFileName), baseline);
            Assert.Equal(LiveMpvConfig.Content, await File.ReadAllTextAsync(live));
            Assert.Equal(LiveMpvConfig.BaselineContent, await File.ReadAllTextAsync(baseline));

            await File.WriteAllTextAsync(live, "custom");
            await LiveMpvConfig.EnsureFileAsync(directory);
            Assert.Equal("custom", await File.ReadAllTextAsync(live));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
