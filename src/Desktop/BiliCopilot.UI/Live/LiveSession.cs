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
    /// 已读取房间详情.
    /// </summary>
    Ready,

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
/// 直播房间详情的状态：房间信息、失败原因与请求代际。
/// </summary>
/// <remarks>
/// 纯状态容器：不依赖 WinUI，也不直接发请求。连接器在 UI 线程发起请求，
/// 每次 <see cref="BeginRequest"/> 都会让上一代请求失效，迟到的结果不得写入。
/// </remarks>
public sealed class LiveSession
{
    private CancellationTokenSource? _pending;
    private int _generation;
    private string? _titleOverride;

    /// <summary>
    /// Gets 首次进入时由卡片带来的房间信息（可能不完整）.
    /// </summary>
    public LiveInformation? Information { get; private set; }

    /// <summary>
    /// Gets 房间详情.
    /// </summary>
    public LivePlayerView? Page { get; private set; }

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
    /// Gets 房间标题；实时标题事件优先，其次详情，最后是卡片标题.
    /// </summary>
    public string? Title => _titleOverride ?? Page?.Information.Identifier.Title ?? Information?.Identifier.Title;

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
        State = LiveSessionState.Ready;
        return true;
    }

    /// <summary>
    /// 写入房间标题变更事件带来的新标题（#198）；不改变房间标识与播放状态.
    /// </summary>
    /// <param name="title">新标题.</param>
    public void SetTitle(string? title)
        => _titleOverride = string.IsNullOrWhiteSpace(title) ? _titleOverride : title;

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
        _titleOverride = default;
        Page = default;
        State = LiveSessionState.Idle;
        FailureKind = LiveFailureKind.None;
        FailureDetail = default;
    }
}