// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.Live;
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

/// <summary>
/// 直播媒体源解析器：向内核请求直播间详情与播放地址，拼出带登录态的 mpv 媒体源。
/// </summary>
/// <remarks>
/// 直播没有时长与终点，因此不读取任何历史/续播设置，也不在失败时复用旧地址。
/// 地址候选排序只做最小能力排序，完整的画质/线路切换与断流恢复属于 #194：
/// 届时在 <c>RefreshSourceAsync</c> 里接 <c>PlaybackRefreshController</c>，
/// 并扩展 <see cref="LivePlayUrlSelector.Order"/> 即可，不要另起一套生命周期。
/// </remarks>
internal sealed partial class LiveMediaSourceResolver(
    IPlayerService playerService,
    IBiliCookiesResolver cookiesResolver,
    ILogger<LiveMediaSourceResolver> logger) : MediaSourceResolverBase
{
    /// <summary>
    /// 内核未指定清晰度时的默认值（B 站直播的蓝光原画标识）.
    /// </summary>
    internal const int DefaultQuality = 400;

    private MediaSnapshot _snapshot;
    private LivePlayerView? _view;
    private LiveMediaInformation? _playbackInfo;
    private Task? _loadTask;
    private CancellationTokenSource? _initCts;
    private int _sourceGeneration;

    /// <summary>
    /// 绑定本次观看的媒体快照，并让上一房间的进行中请求失效。
    /// </summary>
    /// <param name="snapshot">直播媒体快照.</param>
    public void Initialize(MediaSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _sourceGeneration++;
        _initCts?.Cancel();
        _initCts = default;
        _snapshot = snapshot;
        _view = default;
        _playbackInfo = default;
        _loadTask = default;
    }

    /// <summary>
    /// 让进行中的地址请求失效；关闭窗口时由播放器调用。
    /// </summary>
    internal void Cancel()
    {
        _sourceGeneration++;
        _initCts?.Cancel();
        // 取消后允许重新取址；旧任务因代际不符不会写回。
        _loadTask = default;
        _playbackInfo = default;
        _view = default;
    }

    /// <inheritdoc/>
    public override async Task<MpvMediaSource> GetSourceAsync()
    {
        if (_snapshot?.Live is null)
        {
            throw new InvalidOperationException("直播信息未初始化.");
        }

        var generation = _sourceGeneration;
        var volume = SettingsToolkit.ReadLocalSetting(SettingNames.PlayerVolume, 100d);
        var cookies = cookiesResolver.GetCookieString();
        var headers = new Dictionary<string, string>
        {
            { "Referer", InternalHttpExtensions.LiveReferer },
        };
        if (!string.IsNullOrEmpty(cookies))
        {
            headers.Add("Cookie", cookies);
        }

        var options = new MpvPlayOptions
        {
            WindowHandle = WindowHandle,
            InitialVolume = volume,
            InitialSpeed = 1d,
            UserAgent = InternalHttpExtensions.VideoUserAgent,
            HttpHeaders = headers,
            EnableCookies = true,
        };

        await LoadMediaInfoAsync();
        if (generation != _sourceGeneration)
        {
            throw new OperationCanceledException();
        }

        var target = LivePlayUrlSelector.Select(_playbackInfo?.Lines)
            ?? throw new InvalidOperationException("无法获取到可用的直播播放地址.");
        var title = _view?.Information.Identifier.Title ?? _snapshot.Live.Identifier.Title;
        options.MediaName = title;
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("直播 {RoomId} 选择 {Protocol}/{Codec}/{Quality}", _snapshot.Live.Identifier.Id, target.Protocol, target.CodecName, target.Quality);
        }
        return new MpvMediaSource(target.Url, _snapshot.Live.Identifier.Id, title, options);
    }

    /// <inheritdoc/>
    public override IMpvMediaSourceResolver Clone() => throw new NotImplementedException();

    /// <summary>
    /// 读取直播间详情与播放地址；同一房间只请求一次，失败后允许重试。
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    internal async Task LoadMediaInfoAsync()
    {
        if (_snapshot?.Live is null)
        {
            throw new InvalidOperationException("直播信息未初始化.");
        }

        if (_playbackInfo is not null)
        {
            return;
        }

        var generation = _sourceGeneration;
        _loadTask ??= LoadPlaybackInfoAsync();
        try
        {
#pragma warning disable VSTHRD003 // 解析器持有该地址请求，等待它不会阻塞 UI 线程。
            await _loadTask;
#pragma warning restore VSTHRD003
        }
        catch
        {
            if (generation == _sourceGeneration)
            {
                _loadTask = default;
            }

            throw;
        }
    }

    private async Task LoadPlaybackInfoAsync()
    {
        var generation = _sourceGeneration;
        _initCts = new CancellationTokenSource();
        var token = _initCts.Token;
        var view = await playerService.GetLivePageDetailAsync(_snapshot.Live!.Identifier, token);
        var information = await playerService.GetLivePlayDetailAsync(
            _snapshot.Live.Identifier,
            _snapshot.PreferQuality ?? DefaultQuality,
            audioOnly: false,
            token);
        if (generation != _sourceGeneration)
        {
            throw new OperationCanceledException();
        }

        if (information is null)
        {
            throw new InvalidOperationException("直播播放信息为空，房间可能已下播或地址已失效.");
        }

        _view = view;
        _playbackInfo = information;
    }
}
