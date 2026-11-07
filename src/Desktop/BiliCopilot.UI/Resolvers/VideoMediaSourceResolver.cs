// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.PlaybackRouting;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using Richasy.MpvKernel.Core.Models;
using Richasy.MpvKernel.Player;
using Richasy.MpvKernel.Player.Models;

namespace BiliCopilot.UI.Resolvers;

internal sealed partial class VideoMediaSourceResolver(IPlayerService playerService, IBiliCookiesResolver cookiesResolver, ILogger<VideoMediaSourceResolver> logger) : MediaSourceResolverBase
{
    private MediaSnapshot _snapshot;
    private VideoPart _part;
    private Task? _loadTask;
    private CancellationTokenSource? _initCts;
    private DashMediaInformation? _playbackInfo;
    private int _sourceGeneration;
    private int? _requiredQuality;
    private int? _selectedQuality;
    private string? _selectedCodec;
    private string? _codec;
    private string? _previousVideoHost;
    private string? _previousAudioHost;
    private string? _videoHost;
    private string? _audioHost;

    public void Initialize(MediaSnapshot snapshot, VideoPart part)
    {
        _sourceGeneration++;
        _requiredQuality = _selectedQuality = null;
        _selectedCodec = null;
        _codec = null;
        _previousVideoHost = _previousAudioHost = _videoHost = _audioHost = null;
        _snapshot = snapshot;
        _part = part;
        _playbackInfo = default;
        _loadTask = null;
        _initCts?.Cancel();
    }

    internal void ResetSnapshot(MediaSnapshot snapshot)
    {
        _sourceGeneration++;
        _snapshot = snapshot;
    }

    internal PlayerFormatInformation? GetCurrentFormat()
    {
        // 作者例外：UP 主本人可以看到未解锁清晰度，其他人只看已解锁的。
        var isVideoAuthor = _snapshot.Video.Publisher?.User?.Id == this.Get<AccountViewModel>().MyProfile.User.Id;
        var selection = QualitySelectionAdapter.SelectFormat(
            _requiredQuality is { } quality ? _playbackInfo?.Formats?.Where(p => p.Quality == quality).ToList() : _playbackInfo?.Formats,
            isVideoAuthor ? QualityEntitlement.VideoAuthor : QualityEntitlement.Standard,
            _snapshot.PreferQuality,
            SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.PreferQuality, PreferQualityType.Auto),
            SettingsToolkit.ReadLocalSetting(SettingNames.LastSelectedVideoQuality, 0));
        if (!selection.HasSelection)
        {
            // 规格要求把“不可用原因”留给排查：这里只记录，不改变选择行为。
            logger.LogWarning("未能选择清晰度（{Title}）：{Reason}", _snapshot.Video.Identifier.Title, selection.Reason);
        }

        return selection.Format;
    }

    public override async Task<MpvMediaSource> RefreshSourceAsync(CancellationToken cancellationToken)
    {
        var quality = _selectedQuality ?? GetCurrentFormat()?.Quality
            ?? throw new InvalidOperationException("当前清晰度不可用，无法刷新播放。");
        var generation = ++_sourceGeneration;
        _initCts?.Cancel();
        // A private request keeps the current playable cache intact on failure.
        var fresh = new VideoMediaSourceResolver(playerService, cookiesResolver, logger);
        fresh.Initialize(_snapshot, _part);
        fresh.WindowHandle = WindowHandle;
        fresh._requiredQuality = quality;
        fresh._codec = _selectedCodec ?? AppToolkit.GetPreferCodecId();
        fresh._previousVideoHost = _videoHost;
        fresh._previousAudioHost = _audioHost;
        try
        {
            var source = await fresh.GetSourceAsync().WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _sourceGeneration)
            {
                throw new OperationCanceledException();
            }

            _playbackInfo = fresh._playbackInfo;
            _previousVideoHost = fresh._previousVideoHost;
            _previousAudioHost = fresh._previousAudioHost;
            _videoHost = fresh._videoHost;
            _audioHost = fresh._audioHost;
            return source;
        }
        finally
        {
            fresh._sourceGeneration++;
            fresh._initCts?.Cancel();
            fresh._initCts?.Dispose();
        }
    }

    public override IMpvMediaSourceResolver Clone() => throw new NotImplementedException();

    public override async Task<MpvMediaSource> GetSourceAsync()
    {
        var publishDimensions = PublishDisplayDimensionsAsync;
        var generation = _sourceGeneration;
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
        var options = new MpvPlayOptions
        {
            WindowHandle = WindowHandle,
            InitialVolume = volume,
            InitialSpeed = speed,
            UserAgent = userAgent,
            HttpHeaders = headers,
            EnableCookies = true,
            MediaName = _snapshot.Video.Identifier.Title,
        };

        await LoadMediaInfoAsync();
        if (generation != _sourceGeneration)
        {
            throw new OperationCanceledException();
        }

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

        return new MpvMediaSource(fileUrl, _snapshot.Video.Identifier.Id, _snapshot.Video.Identifier.Title, options);
    }

    public async Task<List<PlayerFormatInformation>?> GetSourcesAsync()
    {
        await LoadMediaInfoAsync();
        return [.. _playbackInfo?.Formats];
    }

    /// <summary>
    /// 获取视频章节列表.
    /// </summary>
    /// <returns>章节列表，视频未标记章节时为空.</returns>
    /// <remarks>
    /// 数据来自播放接口的 view_points 字段（由 UP 主在 B 站上标记），
    /// 与容器内嵌章节无关，因此 B 站的 DASH 流也能拿到.
    /// </remarks>
    internal async Task<List<VideoChapter>> GetChaptersAsync()
    {
        if (_snapshot is null || _part is null)
        {
            return [];
        }

        try
        {
            var chapters = await playerService.GetVideoChaptersAsync(
                _snapshot.Video.Identifier,
                Convert.ToInt64(_part.Identifier.Id));
            return chapters is null ? [] : [.. chapters];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "获取章节信息失败，将不展示分段进度.");
            return [];
        }
    }

    internal async Task LoadMediaInfoAsync()
    {
        if (_snapshot is null || _part is null)
        {
            throw new InvalidOperationException("必须先初始化媒体信息.");
        }

        if (_playbackInfo is not null)
        {
            return;
        }

        var generation = _sourceGeneration;
        _loadTask ??= LoadPlaybackInfoAsync();
        try
        {
#pragma warning disable VSTHRD003 // This resolver owns the asynchronous request.
            await _loadTask;
#pragma warning restore VSTHRD003
        }
        catch
        {
            if (generation == _sourceGeneration)
            {
                _loadTask = null;
            }

            throw;
        }
    }

    private async Task LoadPlaybackInfoAsync()
    {
        var generation = _sourceGeneration;
        _initCts = new CancellationTokenSource();
        var information = await playerService.GetVideoPlayDetailAsync(_snapshot.Video.Identifier, Convert.ToInt64(_part.Identifier.Id), _initCts.Token);
        if (generation != _sourceGeneration)
        {
            throw new OperationCanceledException();
        }

        _playbackInfo = information;
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
        var preferCodec = _codec ?? AppToolkit.GetPreferCodecId();
        var vSeg = videoSegments?.FirstOrDefault(p => p.Id == selectedFormat.Quality.ToString() && p.Codecs.Contains(preferCodec))
            ?? videoSegments?.FirstOrDefault(p => p.Id == selectedFormat.Quality.ToString());
        if (_requiredQuality.HasValue && (vSeg is null || !vSeg.Codecs.Contains(preferCodec)))
        {
            throw new InvalidOperationException("当前清晰度或编码已不可用，请重新选择清晰度。");
        }

        var aSeg = audioSegments?.FirstOrDefault(p => p.Id == maxAudioQuality.ToString());

        var preference = PlaybackRouteSettings.ReadPreference();
        var withoutP2P = SettingsToolkit.ReadLocalSetting(SettingNames.PlayWithoutP2P, false);
        var videoUrl = PlaybackRouteSelector.Select(vSeg?.BaseUrl, vSeg?.BackupUrls, preference, withoutP2P, _previousVideoHost);
        var audioUrl = PlaybackRouteSelector.Select(aSeg?.BaseUrl, aSeg?.BackupUrls, preference, withoutP2P, _previousAudioHost);

        SettingsToolkit.WriteLocalSetting(SettingNames.LastSelectedVideoQuality, selectedFormat.Quality);
        if (_requiredQuality.HasValue && string.IsNullOrEmpty(videoUrl))
        {
            throw new InvalidOperationException("当前清晰度已不可用，请重新选择清晰度。");
        }

        _selectedQuality = selectedFormat.Quality;
        _selectedCodec = vSeg?.Codecs;
        _videoHost = videoUrl is null ? null : new Uri(videoUrl).Host;
        _audioHost = audioUrl is null ? null : new Uri(audioUrl).Host;
        return (videoUrl, audioUrl, vSeg);
    }
}
