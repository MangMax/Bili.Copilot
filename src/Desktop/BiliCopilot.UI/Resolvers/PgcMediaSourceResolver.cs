// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.PlaybackRouting;
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
    private CancellationTokenSource? _initCts;
    private Task? _loadTask;
    private DashMediaInformation? _playbackInfo;

    /// <summary>
    /// 上一次播放信息请求被判定为不可用的原因.
    /// </summary>
    internal PgcPlaybackUnavailableReason? UnavailableReason { get; private set; }

    /// <summary>
    /// 当前拿到的是否只能是试看结果.
    /// </summary>
    internal bool IsPreviewPlayback => _playbackInfo?.IsPreview == true;

    public void Initialize(MediaSnapshot snapshot, EpisodeInformation episode)
    {
        _snapshot = snapshot;
        _episode = episode;
        _playbackInfo = default;
        _loadTask = default;
        UnavailableReason = default;
        _initCts?.Cancel();
        _initCts = default;
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
        var (videoUrl, audioUrl) = GetPlayUrls();
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

        if (string.IsNullOrEmpty(fileUrl))
        {
            // 仅可试看时服务端只给 durl，没有 DASH 分轨。
            fileUrl = _playbackInfo?.PreviewUrls?.FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? string.Empty;
        }

        return new MpvMediaSource(fileUrl, id, title, options);
    }

    /// <summary>
    /// 预取播放信息，把“不可用”的原因留给调用方决定如何提示.
    /// </summary>
    /// <returns>是否拿到了可播放的信息.</returns>
    internal async Task<bool> TryPrepareAsync()
    {
        try
        {
            await LoadMediaInfoAsync();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "准备 {Title} 的播放信息失败", _episode?.Identifier.Title);
            return false;
        }
    }

    internal async Task<List<PlayerFormatInformation>> GetSourcesAsync()
    {
        await LoadMediaInfoAsync();
        return _playbackInfo?.Formats is { Count: > 0 } formats ? [.. formats] : [];
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

        _loadTask ??= LoadPlaybackInfoAsync();
#pragma warning disable VSTHRD003 // 请求由本实例发起，等待它不会阻塞 UI 线程.
        await _loadTask;
#pragma warning restore VSTHRD003
    }

    private async Task LoadPlaybackInfoAsync()
    {
        _initCts = new CancellationTokenSource();
        try
        {
            var cid = _episode.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.Cid);
            var seasonType = _episode.GetExtensionIfNotNull<string>(EpisodeExtensionDataId.SeasonType);
            var information = await playerService.GetPgcPlayDetailAsync(cid.ToString(), _episode.Identifier.Id, seasonType, _initCts.Token);
            _playbackInfo = information;
            UnavailableReason = default;

        }
        catch (PgcPlaybackUnavailableException ex)
        {
            // 服务端明确判定不可用：保留原因，允许上层区分提示.
            UnavailableReason = ex.Reason;
            _playbackInfo = default;
            _loadTask = default;
            var unavailableTitle = _episode.Identifier.Title;
            var reason = ex.Reason;
            logger.LogWarning(ex, "获取 {Title} 播放信息不可用：{Reason}", unavailableTitle, reason);
            throw;
        }
        catch (Exception ex)
        {
            _playbackInfo = default;
            _loadTask = default;
            logger.LogError(ex, $"获取 {_episode.Identifier.Title} 播放信息失败");
            throw;
        }
    }

    private (string? video, string? audio) GetPlayUrls()
    {
        if (_playbackInfo is null)
        {
            return (default, default);
        }

        var videoSegments = _playbackInfo.Videos;
        var audioSegments = _playbackInfo.Audios;
        var selectedFormat = GetCurrentFormat();
        if (selectedFormat is null)
        {
            return (default, default);
        }

        var maxAudioQuality = audioSegments?.Max(p => Convert.ToInt32(p.Id));
        var preferCodec = AppToolkit.GetPreferCodecId();
        var vSeg = videoSegments?.FirstOrDefault(p => p.Id == selectedFormat.Quality.ToString() && p.Codecs.Contains(preferCodec))
            ?? videoSegments?.FirstOrDefault(p => p.Id == selectedFormat.Quality.ToString());
        var aSeg = audioSegments?.FirstOrDefault(p => p.Id == maxAudioQuality.ToString());

        var preference = PlaybackRouteSettings.ReadPreference();
        var withoutP2P = SettingsToolkit.ReadLocalSetting(SettingNames.PlayWithoutP2P, false);
        var videoUrl = PlaybackRouteSelector.Select(vSeg?.BaseUrl, vSeg?.BackupUrls, preference, withoutP2P);
        var audioUrl = PlaybackRouteSelector.Select(aSeg?.BaseUrl, aSeg?.BackupUrls, preference, withoutP2P);

        SettingsToolkit.WriteLocalSetting(SettingNames.LastSelectedPgcQuality, selectedFormat.Quality);
        return (videoUrl, audioUrl);
    }
}
