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
/// 候选排序、失败降级与刷新都集中在这里：候选按 <see cref="LivePlayUrlSelector"/> 排序，
/// 失败后依次尝试同一候选的多条地址/下一个候选，候选耗尽则由
/// <see cref="GetRecoverySourceAsync"/> 重新请求 playurl；真正的准入与重开仍由
/// <c>PlaybackRefreshController</c> 持有，不另起生命周期。
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

    private readonly LiveRecoverySequencer _recovery = new();
    private MediaSnapshot _snapshot;
    private LivePlayerView? _view;
    private LiveMediaInformation? _playbackInfo;
    private IReadOnlyList<LivePlaybackCandidate> _candidates = [];
    private int _candidateIndex = -1;
    private int _urlIndex = -1;
    private string? _preferredLineKey;
    private int? _selectedQuality;
    private Task? _loadTask;
    private CancellationTokenSource? _initCts;
    private CancellationTokenSource? _refreshCts;
    private int _sourceGeneration;
    private LiveRecoveryAction _pendingRecovery = LiveRecoveryAction.None;

    /// <summary>
    /// Gets 最近一次预取失败的原因。
    /// </summary>
    internal LiveFailureKind FailureKind { get; private set; }

    /// <summary>
    /// Gets 最近一次播放失败分类（用于重试仍失败时的可读提示）.
    /// </summary>
    internal LivePlaybackFailureKind LastPlaybackFailure { get; private set; }

    /// <summary>
    /// Gets 当前播放信息（清晰度与线路候选）.
    /// </summary>
    internal LiveMediaInformation? PlaybackInfo => _playbackInfo;

    /// <summary>
    /// Gets 当前排序后的线路候选.
    /// </summary>
    internal IReadOnlyList<LivePlaybackCandidate> Candidates => _candidates;

    /// <summary>
    /// Gets 当前选中的线路候选.
    /// </summary>
    internal LivePlaybackCandidate? CurrentCandidate
        => _candidateIndex >= 0 && _candidateIndex < _candidates.Count ? _candidates[_candidateIndex] : default;

    /// <summary>Gets 当前实际取流地址的域名，包括候选内的备用地址.</summary>
    internal string? CurrentHost => GetCurrentTarget()?.Host;

    /// <summary>复用连接器已读取的本房间详情.</summary>
    internal void SetPageDetail(LivePlayerView view) => _view = view;

    /// <summary>
    /// Gets 当前会话选中的清晰度标识.
    /// </summary>
    internal int SelectedQuality => ResolveSelectedQuality();

    /// <summary>
    /// Gets 当前线路偏好（首选地址域名）；仅在内存中保留，不落盘.
    /// </summary>
    internal string? PreferredLineKey => _preferredLineKey;

    /// <summary>
    /// Gets 是否有进行中的刷新请求（关闭/换房会取消它）.
    /// </summary>
    internal bool IsRefreshPending => _refreshCts is not null;

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
        _candidates = [];
        _candidateIndex = -1;
        _urlIndex = -1;
        _preferredLineKey = default;
        _loadTask = default;
        _recovery.Reset();
        _pendingRecovery = LiveRecoveryAction.None;
        FailureKind = LiveFailureKind.None;
        LastPlaybackFailure = LivePlaybackFailureKind.None;
        var savedQuality = SettingsToolkit.ReadLocalSetting(SettingNames.LastSelectedLiveQuality, 0);
        _selectedQuality = savedQuality > 0 ? savedQuality : null;
    }

    /// <summary>
    /// 让进行中的地址请求失效；关闭窗口时由播放器调用。
    /// </summary>
    internal void Cancel()
    {
        _sourceGeneration++;
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = default;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = default;
        // 取消后允许重新取址；旧任务因代际不符不会写回。
        _loadTask = default;
        _playbackInfo = default;
        _view = default;
        _candidates = [];
        _candidateIndex = -1;
        _urlIndex = -1;
        _pendingRecovery = LiveRecoveryAction.None;
        _recovery.Reset();
    }

    /// <summary>
    /// 记住用户选择的清晰度；只持久化清晰度标识，绝不保存签名地址。
    /// </summary>
    /// <param name="quality">清晰度标识.</param>
    internal void SelectQuality(int quality)
    {
        if (quality <= 0)
        {
            return;
        }

        _selectedQuality = quality;
        SettingsToolkit.WriteLocalSetting(SettingNames.LastSelectedLiveQuality, quality);
    }

    /// <summary>
    /// 记住用户选择的线路（首选地址域名）；线路偏好只在内存中跨刷新保留。
    /// </summary>
    /// <param name="key">候选的稳定标识.</param>
    internal void SelectLine(string? key) => _preferredLineKey = key;

    /// <inheritdoc/>
    public override async Task<MpvMediaSource> GetSourceAsync()
    {
        if (_snapshot?.Live is null)
        {
            throw new InvalidOperationException("直播信息未初始化.");
        }

        var generation = _sourceGeneration;
        var options = CreatePlayOptions();
        await LoadMediaInfoAsync();
        if (generation != _sourceGeneration)
        {
            throw new OperationCanceledException();
        }

        EnsureCandidates();
        var target = GetCurrentTarget()
            ?? throw new InvalidOperationException("无法获取到可用的直播播放地址.");
        var title = _view?.Information.Identifier.Title ?? _snapshot.Live.Identifier.Title;
        options.MediaName = title;
        _recovery.BeginAttempt();
        _pendingRecovery = LiveRecoveryAction.None;
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "直播 {RoomId} 选择 {Protocol}/{Codec}/{Quality}/{Container} {Host}",
                _snapshot.Live.Identifier.Id,
                target.Protocol,
                target.CodecName,
                target.Quality,
                target.Container,
                target.Host);
        }

        return new MpvMediaSource(target.Url, _snapshot.Live.Identifier.Id, title, options);
    }

    /// <inheritdoc/>
    public override async Task<MpvMediaSource> RefreshSourceAsync(CancellationToken cancellationToken)
    {
        if (_snapshot?.Live is null)
        {
            throw new InvalidOperationException("直播信息未初始化.");
        }

        var quality = ResolveSelectedQuality();
        var generation = _sourceGeneration;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        var refreshCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _refreshCts = refreshCts;
        var token = refreshCts.Token;
        var fresh = new LiveMediaSourceResolver(playerService, cookiesResolver, logger);
        fresh.Initialize(_snapshot);
        fresh.WindowHandle = WindowHandle;
        fresh._view = _view;
        // 只继承清晰度与线路偏好；绝不继承签名地址、起始位置或倍速。
        fresh._selectedQuality = quality;
        fresh._preferredLineKey = _preferredLineKey;
        try
        {
            var source = await fresh.GetSourceAsync().WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (generation != _sourceGeneration)
            {
                throw new OperationCanceledException();
            }

            _sourceGeneration++;
            _initCts?.Cancel();
            _view = fresh._view;
            _playbackInfo = fresh._playbackInfo;
            _candidates = fresh._candidates;
            _candidateIndex = fresh._candidateIndex;
            _urlIndex = fresh._urlIndex;
            _preferredLineKey = fresh._preferredLineKey;
            _pendingRecovery = LiveRecoveryAction.None;
            _recovery.Reset();
            LastPlaybackFailure = LivePlaybackFailureKind.None;
            return source;
        }
        finally
        {
            fresh._sourceGeneration++;
            fresh._initCts?.Cancel();
            fresh._initCts?.Dispose();
            if (ReferenceEquals(_refreshCts, refreshCts))
            {
                _refreshCts = default;
            }

            refreshCts.Dispose();
        }
    }

    /// <summary>
    /// 根据最近一次失败与已尝试的候选，返回下一条可播放地址。
    /// </summary>
    /// <remarks>
    /// 候选耗尽时会重新请求 playurl（签名地址会过期）；重新取址后仍失败则抛出
    /// <see cref="LivePlaybackUnavailableException"/>，由界面展示可读错误并等待手动重试。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>下一条媒体源.</returns>
    internal async Task<MpvMediaSource> GetRecoverySourceAsync(CancellationToken cancellationToken)
    {
        var action = _pendingRecovery;
        _pendingRecovery = LiveRecoveryAction.None;
        if (action == LiveRecoveryAction.AdvanceCandidate && TryMoveNext())
        {
            return await GetSourceAsync();
        }

        if (action is LiveRecoveryAction.AdvanceCandidate or LiveRecoveryAction.ReRequestPlayUrl)
        {
            await ReloadPlaybackInfoAsync(cancellationToken);
            return await GetSourceAsync();
        }

        if (action == LiveRecoveryAction.ManualRetry)
        {
            throw new LivePlaybackUnavailableException(
                LastPlaybackFailure,
                "直播地址多次重试仍不可用，请稍后重试或在浏览器打开。");
        }

        return await GetSourceAsync();
    }

    /// <summary>
    /// 记录一次播放失败并决定下一步动作；同一次尝试内的后续错误（如 HTTP 错误后的
    /// VoInitFailed）会被忽略。
    /// </summary>
    /// <param name="kind">失败分类.</param>
    /// <returns>恢复动作.</returns>
    internal LiveRecoveryAction NotifyPlaybackFailure(LivePlaybackFailureKind kind)
    {
        if (kind is LivePlaybackFailureKind.None or LivePlaybackFailureKind.Cancelled)
        {
            return LiveRecoveryAction.None;
        }

        LastPlaybackFailure = kind;
        _pendingRecovery = _recovery.Report(kind, HasNextTarget());
        if (_pendingRecovery != LiveRecoveryAction.None)
        {
            // 这条线路已经失败，别再让旧偏好把选择拉回去；让恢复流程换到下一条候选。
            _preferredLineKey = null;
        }

        return _pendingRecovery;
    }

    /// <inheritdoc/>
    public override IMpvMediaSourceResolver Clone() => throw new NotImplementedException();

    /// <summary>
    /// 在创建播放器之前预取地址：播放器内核会把取源异常吞成一条日志，
    /// 只有在这里拦住才能把可读原因交给用户。
    /// </summary>
    /// <returns>地址可用时为 true；失败原因见 <see cref="FailureKind"/>.</returns>
    internal async Task<bool> TryPrepareAsync()
    {
        try
        {
            await LoadMediaInfoAsync();
            EnsureCandidates();
            FailureKind = LiveFailureKind.None;
            return _candidates.Count > 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            FailureKind = LiveFailureClassifier.Classify(ex);
            return false;
        }
    }

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
            await _loadTask;
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

    private MpvPlayOptions CreatePlayOptions()
    {
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

        return new MpvPlayOptions
        {
            WindowHandle = WindowHandle,
            InitialVolume = volume,
            InitialSpeed = 1d,
            UserAgent = InternalHttpExtensions.VideoUserAgent,
            HttpHeaders = headers,
            EnableCookies = true,
        };
    }

    private async Task LoadPlaybackInfoAsync()
    {
        var generation = _sourceGeneration;
        _initCts?.Dispose();
        _initCts = new CancellationTokenSource();
        var token = _initCts.Token;
        var view = _view ?? await playerService.GetLivePageDetailAsync(_snapshot.Live!.Identifier, token);
        var information = await playerService.GetLivePlayDetailAsync(
            _snapshot.Live.Identifier,
            ResolveSelectedQuality(),
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
        EnsureCandidates();
    }

    private async Task ReloadPlaybackInfoAsync(CancellationToken cancellationToken)
    {
        var generation = ++_sourceGeneration;
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _initCts.Token;
        _loadTask = default;
        _playbackInfo = default;
        _candidates = [];
        _candidateIndex = -1;
        _urlIndex = -1;
        try
        {
            var information = await playerService.GetLivePlayDetailAsync(
                _snapshot.Live!.Identifier,
                ResolveSelectedQuality(),
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

            _playbackInfo = information;
            EnsureCandidates();
            _recovery.BeginAttempt();
            _pendingRecovery = LiveRecoveryAction.None;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastPlaybackFailure = LivePlaybackFailureClassifier.ClassifyPlayback(ex);
            if (generation == _sourceGeneration)
            {
                _loadTask = default;
            }

            throw;
        }
    }

    private void EnsureCandidates()
    {
        if (_playbackInfo is null)
        {
            _candidates = [];
            _candidateIndex = -1;
            _urlIndex = -1;
            return;
        }

        if (_candidateIndex >= 0 && _candidateIndex < _candidates.Count)
        {
            return;
        }

        _candidates = LivePlayUrlSelector.OrderCandidates(_playbackInfo.Lines);
        (_candidateIndex, _urlIndex) = FindPreferredTarget();
    }

    private (int Candidate, int Url) FindPreferredTarget()
    {
        if (!string.IsNullOrEmpty(_preferredLineKey))
        {
            for (var i = 0; i < _candidates.Count; i++)
            {
                var targets = _candidates[i].Targets;
                for (var j = 0; j < targets.Count; j++)
                {
                    if (string.Equals(targets[j].Host, _preferredLineKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return (i, j);
                    }
                }
            }
        }

        return _candidates.Count == 0 ? (-1, -1) : (0, 0);
    }

    private LivePlaybackTarget? GetCurrentTarget()
        => _candidateIndex >= 0 && _candidateIndex < _candidates.Count
            ? _candidates[_candidateIndex].Targets[_urlIndex]
            : default;

    private bool HasNextTarget()
    {
        if (_candidateIndex < 0 || _candidateIndex >= _candidates.Count)
        {
            return false;
        }

        if (_urlIndex + 1 < _candidates[_candidateIndex].Targets.Count)
        {
            return true;
        }

        return _candidateIndex + 1 < _candidates.Count;
    }

    private bool TryMoveNext()
    {
        if (_candidateIndex < 0 || _candidateIndex >= _candidates.Count)
        {
            return false;
        }

        if (_urlIndex + 1 < _candidates[_candidateIndex].Targets.Count)
        {
            _urlIndex++;
            return true;
        }

        if (_candidateIndex + 1 < _candidates.Count)
        {
            _candidateIndex++;
            _urlIndex = 0;
            return true;
        }

        return false;
    }

    private int ResolveSelectedQuality()
        => _selectedQuality ?? _snapshot?.PreferQuality ?? DefaultQuality;
}