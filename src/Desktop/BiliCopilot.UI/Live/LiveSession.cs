// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 一次直播观看的状态。
/// </summary>
public enum LiveSessionState
{
    /// <summary>
    /// 尚未开始.
    /// </summary>
    Idle,

    /// <summary>
    /// 正在请求房间信息或播放地址.
    /// </summary>
    Loading,

    /// <summary>
    /// 已拿到可播放地址.
    /// </summary>
    Ready,

    /// <summary>
    /// 已转交浏览器.
    /// </summary>
    BrowserFallback,

    /// <summary>
    /// 播放失败.
    /// </summary>
    Faulted,

    /// <summary>
    /// 会话已关闭.
    /// </summary>
    Closed,
}

/// <summary>
/// 一次请求的代际与取消令牌。
/// </summary>
/// <param name="Generation">本次请求的代际.</param>
/// <param name="CancellationToken">本次请求的取消令牌.</param>
public readonly record struct LiveRequest(int Generation, CancellationToken CancellationToken);

/// <summary>
/// 一次直播观看的完整状态：房间信息、线路候选、播放目标、失败原因与请求代际。
/// </summary>
/// <remarks>
/// 纯状态容器：不依赖 WinUI，也不直接发请求。连接器在 UI 线程发起请求，
/// 每次 <see cref="BeginRequest"/> 都会让上一代请求失效，迟到的结果不得写入。
/// <para>
/// 扩展接缝：
/// #194 画质/线路/断流恢复——在 <see cref="Playback"/> 上记录候选与选择，恢复动作仍走
///       PlaybackRefreshController，不新建生命周期。
/// #196 弹幕准入——会话就绪后由连接器把实时事件交给准入层，再走 AddDanmaku。
/// #197 聊天侧栏——会话只暴露房间/连接状态，消息有界列表由面板自持。
/// #198 房间状态/实时事件——标题/在线人数/上下播/PLAYURL_RELOAD 写回本类与 PropertiesUpdated。
/// </para>
/// </remarks>
public sealed class LiveSession
{
    private CancellationTokenSource? _pending;
    private int _generation;

    /// <summary>
    /// Gets 首次进入时由卡片带来的房间信息（可能不完整）.
    /// </summary>
    public LiveInformation? Information { get; private set; }

    /// <summary>
    /// Gets 房间详情.
    /// </summary>
    public LivePlayerView? Page { get; private set; }

    /// <summary>
    /// Gets 播放信息（清晰度与线路候选）.
    /// </summary>
    public LiveMediaInformation? Playback { get; private set; }

    /// <summary>
    /// Gets 当前选中的播放地址.
    /// </summary>
    public LivePlaybackTarget? Target { get; private set; }

    /// <summary>
    /// Gets 当前会话状态.
    /// </summary>
    public LiveSessionState State { get; private set; } = LiveSessionState.Idle;

    /// <summary>
    /// Gets 最近一次失败分类.
    /// </summary>
    public LiveFailureKind FailureKind { get; private set; }

    /// <summary>
    /// Gets 最近一次失败的原始信息（用于日志，不直接展示）.
    /// </summary>
    public string? FailureDetail { get; private set; }

    /// <summary>
    /// Gets 当前请求代际.
    /// </summary>
    public int Generation => _generation;

    /// <summary>
    /// Gets 直播间 Id.
    /// </summary>
    public string? RoomId => Page?.Information.Identifier.Id ?? Information?.Identifier.Id;

    /// <summary>
    /// Gets 房间标题；详情未到时使用卡片标题.
    /// </summary>
    public string? Title => Page?.Information.Identifier.Title ?? Information?.Identifier.Title;

    /// <summary>
    /// Gets 主播昵称.
    /// </summary>
    public string? AnchorName => Page?.Information.User?.Name ?? Information?.User?.Name;

    /// <summary>
    /// Gets 是否正在直播.
    /// </summary>
    public bool IsLiving => Page?.Information.GetExtensionIfNotNull<bool?>(LiveExtensionDataId.IsLiving) == true;

    /// <summary>
    /// Gets 分区名称.
    /// </summary>
    public string? TagName => Page?.Tag?.Name;

    /// <summary>
    /// 开始一次新请求；上一代请求会被取消。
    /// </summary>
    /// <returns>本次请求的代际与取消令牌.</returns>
    public LiveRequest BeginRequest()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        _generation++;
        State = LiveSessionState.Loading;
        FailureKind = LiveFailureKind.None;
        FailureDetail = default;
        return new LiveRequest(_generation, _pending.Token);
    }

    /// <summary>
    /// 判断请求是否仍属于当前会话。
    /// </summary>
    /// <param name="generation">请求代际.</param>
    /// <returns>仍为当前请求时为 true.</returns>
    public bool IsCurrent(int generation) => generation == _generation;

    /// <summary>
    /// 写入房间详情。
    /// </summary>
    /// <param name="generation">请求代际.</param>
    /// <param name="view">房间详情.</param>
    /// <returns>写入成功时为 true；迟到请求返回 false.</returns>
    public bool CompletePage(int generation, LivePlayerView view)
    {
        if (!IsCurrent(generation) || view is null)
        {
            return false;
        }

        Page = view;
        return true;
    }

    /// <summary>
    /// 写入播放信息。
    /// </summary>
    /// <param name="generation">请求代际.</param>
    /// <param name="playback">播放信息.</param>
    /// <returns>写入成功时为 true；迟到请求返回 false.</returns>
    public bool CompletePlayback(int generation, LiveMediaInformation playback)
    {
        if (!IsCurrent(generation) || playback is null)
        {
            return false;
        }

        Playback = playback;
        return true;
    }

    /// <summary>
    /// 写入内核返回的最新播放信息（刷新/重新取址后由解析器回填）.
    /// </summary>
    /// <param name="playback">播放信息.</param>
    public void SetPlayback(LiveMediaInformation? playback) => Playback = playback;

    /// <summary>
    /// 写入选中的播放地址。
    /// </summary>
    /// <param name="generation">请求代际.</param>
    /// <param name="target">播放目标.</param>
    /// <returns>写入成功时为 true；迟到请求返回 false.</returns>
    public bool ApplyTarget(int generation, LivePlaybackTarget target)
    {
        if (!IsCurrent(generation) || target is null)
        {
            return false;
        }

        Target = target;
        State = LiveSessionState.Ready;
        return true;
    }

    /// <summary>
    /// 记录一次失败。
    /// </summary>
    /// <param name="generation">请求代际.</param>
    /// <param name="kind">失败分类.</param>
    /// <param name="detail">原始错误信息.</param>
    /// <returns>写入成功时为 true；迟到请求返回 false.</returns>
    public bool Fail(int generation, LiveFailureKind kind, string? detail)
    {
        if (!IsCurrent(generation) || kind == LiveFailureKind.None)
        {
            return false;
        }

        FailureKind = kind;
        FailureDetail = detail;
        State = LiveSessionState.Faulted;
        return true;
    }

    /// <summary>
    /// 标记已转交浏览器。
    /// </summary>
    /// <param name="generation">请求代际.</param>
    /// <returns>写入成功时为 true；迟到请求返回 false.</returns>
    public bool MarkBrowserFallback(int generation)
    {
        if (!IsCurrent(generation))
        {
            return false;
        }

        State = LiveSessionState.BrowserFallback;
        return true;
    }

    /// <summary>
    /// 让进行中的请求失效并关闭会话；用于关闭窗口。
    /// </summary>
    public void CancelPending()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = default;
        _generation++;
        State = LiveSessionState.Closed;
    }

    /// <summary>
    /// 为新的房间重置状态。
    /// </summary>
    /// <param name="information">卡片带来的房间信息.</param>
    public void Reset(LiveInformation? information)
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = default;
        _generation++;
        Information = information;
        Page = default;
        Playback = default;
        Target = default;
        State = LiveSessionState.Idle;
        FailureKind = LiveFailureKind.None;
        FailureDetail = default;
    }
}