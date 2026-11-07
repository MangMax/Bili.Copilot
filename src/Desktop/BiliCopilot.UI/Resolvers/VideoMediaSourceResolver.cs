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
    private TaskCompletionSource<bool>? _initTask;
    private CancellationTokenSource? _initCts;
    private DashMediaInformation? _playbackInfo;

    public void Initialize(MediaSnapshot snapshot, VideoPart part)
    {
        _snapshot = snapshot;
        _part = part;
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
        // 作者例外：UP 主本人可以看到未解锁清晰度，其他人只看已解锁的。
        var isVideoAuthor = _snapshot.Video.Publisher?.User?.Id == this.Get<AccountViewModel>().MyProfile.User.Id;
        var selection = QualitySelectionAdapter.SelectFormat(
            _playbackInfo?.Formats,
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
            _playbackInfo = await playerService.GetVideoPlayDetailAsync(_snapshot.Video.Identifier, Convert.ToInt64(_part.Identifier.Id), _initCts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"获取 {_snapshot.Video.Identifier.Title} 播放信息失败");
            _initTask.TrySetException(new InvalidOperationException("获取 Dash 播放信息失败."));
            return;
        }

        _initTask.TrySetResult(true);
    }

    private (string? video, string? audio) GetPlayUrls()
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

        var preference = PlaybackRouteSettings.ReadPreference();
        var withoutP2P = SettingsToolkit.ReadLocalSetting(SettingNames.PlayWithoutP2P, false);
        var videoUrl = PlaybackRouteSelector.Select(vSeg?.BaseUrl, vSeg?.BackupUrls, preference, withoutP2P);
        var audioUrl = PlaybackRouteSelector.Select(aSeg?.BaseUrl, aSeg?.BackupUrls, preference, withoutP2P);

        SettingsToolkit.WriteLocalSetting(SettingNames.LastSelectedVideoQuality, selectedFormat.Quality);
        return (videoUrl, audioUrl);
    }
}
