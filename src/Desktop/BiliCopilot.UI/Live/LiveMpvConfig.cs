// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 应用自有的直播 mpv 配置与缓冲档位（#199 结论，落在 #194）。
/// </summary>
/// <remarks>
/// #199 的调查结论已作为 #194 的行动项落地，因此这组直播参数保留在应用侧；
/// 不再另开一次 spike，也不再引入低延迟 profile 的额外参数。
/// </remarks>
/// <remarks>
/// 播放器启动时使用 <c>UseConfig=false</c>，用户 mpv.conf 不会生效；因此直播参数必须写成
/// 独立文件，在 <c>MpvPlayer.InitializeAsync</c> 之前用 <c>MpvClient.SetConfigFileAsync</c> 加载，
/// 并在返回其他媒体时加载基线文件还原，避免配置在复用的 <c>MpvClient</c> 上粘滞。
/// <para>刻意不包含：<c>reconnect_at_eof</c>（会掩盖主播下播/被切断）与整体 <c>profile=low-latency</c>
/// （其中 <c>cache-pause=no</c>、<c>audio-buffer=0</c> 会让直播缓冲与观测失真）。</para>
/// </remarks>
public static class LiveMpvConfig
{
    /// <summary>
    /// 直播配置文件名.
    /// </summary>
    public const string FileName = "live-mpv.conf";

    /// <summary>
    /// 离开直播时用于还原 mpv 默认行为的基线配置文件名.
    /// </summary>
    public const string BaselineFileName = "live-mpv-baseline.conf";

    /// <summary>
    /// 直播配置内容：ffmpeg 重连、HLS 从最新分片开始、固定同步与丢帧策略。
    /// </summary>
    public const string Content = """
        # App-owned live profile (#194). Keep EOF reconnection and wholesale low-latency profiles out.
        # ffmpeg HTTP AVOptions forwarded by mpv: reconnect streamed (non-seekable) live streams.
        stream-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_delay_max=5
        # HLS: start at the newest segment and retry a failing segment a few times.
        demuxer-lavf-o=live_start_index=-1,seg_max_retry=3
        # Live sync/drop policy: audio clock, drop late frames at the VO (never freeze the decoder).
        video-sync=audio
        framedrop=vo
        """;

    /// <summary>
    /// 基线内容：只把这四个键还原为 mpv 默认值，避免直播配置粘到视频/PGC。
    /// </summary>
    public const string BaselineContent = """
        stream-lavf-o=
        demuxer-lavf-o=
        video-sync=audio
        framedrop=vo
        """;

    /// <summary>
    /// 直播缓冲前向上限（16 MiB，约数秒，取决于码率）.
    /// </summary>
    public const string DemuxerMaxBytes = "16MiB";

    /// <summary>
    /// 直播不回看，后退缓冲为 0.
    /// </summary>
    public const string DemuxerMaxBackBytes = "0MiB";

    /// <summary>
    /// 直播不写磁盘缓存.
    /// </summary>
    public const bool CacheOnDisk = false;

    /// <summary>
    /// 确保配置存在；已存在时保留用户/上一版本写入的内容。
    /// </summary>
    /// <param name="directory">配置目录（应用 LocalFolder）.</param>
    /// <param name="baseline">是否写基线配置.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>配置文件完整路径.</returns>
    public static async Task<string> EnsureFileAsync(string directory, bool baseline = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var path = Path.Combine(directory, baseline ? BaselineFileName : FileName);
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, baseline ? BaselineContent : Content, cancellationToken);
        }

        return path;
    }
}