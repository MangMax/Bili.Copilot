// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using Richasy.MpvKernel.Core.Models;
using Richasy.MpvKernel.Player;
using Richasy.MpvKernel.Player.Models;

namespace BiliCopilot.UI.Resolvers;

internal sealed partial class PgcMediaSourceResolver(IPlayerService playerService, IBiliCookiesResolver cookiesResolver, ILogger<PgcMediaSourceResolver> logger) : MediaSourceResolverBase
{
    private MediaSnapshot _snapshot;
    private EpisodeInformation _episode;
    private TaskCompletionSource<bool>? _initTask;
    private CancellationTokenSource? _initCts;
    private DashMediaInformation? _playbackInfo;

    public void Initialize(MediaSnapshot snapshot, EpisodeInformation episode)
    {
        _snapshot = snapshot;
        _episode = episode;
        _playbackInfo = default;
        _initTask?.TrySetCanceled();
        _initCts?.Cancel();
    }

    internal void ResetSnapshot(MediaSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    internal PlayerFormatInformation? GetCurrentFormat()
    {
        // PGC 没有作者例外：始终只看当前账号已解锁的清晰度。
        var selection = QualitySelectionAdapter.SelectFormat(
            _playbackInfo?.Formats,
            QualityEntitlement.Standard,
            _snapshot.PreferQuality,
            SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.PreferQuality, PreferQualityType.Auto),
            SettingsToolkit.ReadLocalSetting(SettingNames.LastSelectedVideoQuality, 0));
        if (!selection.HasSelection)
        {
            // 规格要求把“不可用原因”留给排查：这里只记录，不改变选择行为。
            logger.LogWarning("未能选择清晰度（{Title}）：{Reason}", _episode.Identifier.Title, selection.Reason);
        }

        return selection.Format;
    }

    public override IMpvMediaSourceResolver Clone() => throw new NotImplementedException();

    public override async Task<MpvMediaSource> GetSourceAsync()
    {
        var publishDimensions = PublishDisplayDimensionsAsync;
        var volume = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.PlayerVolume, 100d);
        var speed = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.PlayerSpeed, 1d);
        var cookies = cookiesResolver.GetCookieString();
        const string referer = InternalHttpExtensions.VideoReferer;
        const string userAgent = InternalHttpExtensions.VideoUserAgent;
        var headers = new Dictionary<string, string>
        {
            { "Cookie", cookies },
            { "Referer", referer },
        };

        var id = _snapshot.Episode?.Identifier.Id ?? _snapshot.Season?.Identifier.Id;
        var title = _snapshot.Episode?.Identifier.Title ?? _snapshot.Season?.Identifier.Title;
        var options = new MpvPlayOptions
        {
            WindowHandle = WindowHandle,
            InitialVolume = volume,
            InitialSpeed = speed,
            UserAgent = userAgent,
            HttpHeaders = headers,
            EnableCookies = true,
            MediaName = title,
        };

        await LoadMediaInfoAsync();
        var (videoUrl, audioUrl, selectedVideo) = GetPlayUrls();
        var fileUrl = string.Empty;
        if (!string.IsNullOrEmpty(videoUrl))
        {
            fileUrl = videoUrl;
            if (!string.IsNullOrEmpty(audioUrl))
            {
                options.AudioTracks ??= [];
                options.AudioTracks.Add(audioUrl);
            }
        }
        else if (!string.IsNullOrEmpty(audioUrl))
        {
            fileUrl = audioUrl;
        }

        // Use the exact selected stream for this part/episode, never the first part
        // of the overall video or a coarse aspect-ratio category.
        if (publishDimensions is not null && selectedVideo is not null && !string.IsNullOrEmpty(videoUrl))
        {
            await publishDimensions(selectedVideo.Width, selectedVideo.Height);
        }

        return new MpvMediaSource(fileUrl, id, title, options);
    }

    public async Task<List<PlayerFormatInformation>?> GetSourcesAsync()
    {
        await LoadMediaInfoAsync();
        return [.. _playbackInfo?.Formats];
    }

    internal async Task LoadMediaInfoAsync()
    {
        if (_snapshot is null || _episode is null)
        {
            throw new InvalidOperationException("必须先初始化媒体信息.");
        }

        if (_playbackInfo is not null)
        {
            return;
        }

        if (_initTask?.Task.IsCompleted == true)
        {
            return;
        }

        if (_initTask is not null)
        {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            await _initTask.Task;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
            return;
        }

        _initTask = new TaskCompletionSource<bool>();
        _initCts = new CancellationTokenSource();
        try
        {
            var cid = _episode.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.Cid);
            var seasonType = _episode.GetExtensionIfNotNull<string>(EpisodeExtensionDataId.SeasonType);
            _playbackInfo = await playerService.GetPgcPlayDetailAsync(cid.ToString(), _episode.Identifier.Id, seasonType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"获取 {_episode.Identifier.Title} 播放信息失败");
            _initTask.TrySetException(new InvalidOperationException("获取 Dash 播放信息失败."));
            return;
        }

        _initTask.TrySetResult(true);
    }

    private (string? video, string? audio, DashSegmentInformation? selectedVideo) GetPlayUrls()
    {
        var videoSegments = _playbackInfo.Videos;
        var audioSegments = _playbackInfo.Audios;
        var selectedFormat = GetCurrentFormat();
        if (selectedFormat is null)
        {
            throw new InvalidCastException("无法获取到有效的播放地址");
        }

        var maxAudioQuality = audioSegments?.Max(p => Convert.ToInt32(p.Id));
        var preferCodec = AppToolkit.GetPreferCodecId();
        var vSeg = videoSegments?.FirstOrDefault(p => p.Id == selectedFormat.Quality.ToString() && p.Codecs.Contains(preferCodec))
            ?? videoSegments?.FirstOrDefault(p => p.Id == selectedFormat.Quality.ToString());
        var aSeg = audioSegments?.FirstOrDefault(p => p.Id == maxAudioQuality.ToString());

        var videoUrl = vSeg?.BaseUrl;
        var audioUrl = aSeg?.BaseUrl;

        if (SettingsToolkit.ReadLocalSetting(SettingNames.PlayWithoutP2P, false))
        {
            if (vSeg?.BackupUrls is not null)
            {
                string[] videoUrls = [vSeg?.BaseUrl, .. vSeg?.BackupUrls];
                videoUrl = Array.Find(videoUrls, p => !AppToolkit.IsP2PUrl(p)) ?? vSeg?.BaseUrl;
            }

            if (aSeg?.BackupUrls is not null)
            {
                string[] audioUrls = [aSeg?.BaseUrl, .. aSeg?.BackupUrls];
                audioUrl = Array.Find(audioUrls, p => !AppToolkit.IsP2PUrl(p)) ?? aSeg?.BaseUrl;
            }
        }

        SettingsToolkit.WriteLocalSetting(SettingNames.LastSelectedPgcQuality, selectedFormat.Quality);
        return (videoUrl, audioUrl, vSeg);
    }
}
