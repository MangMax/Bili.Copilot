// Copyright (c) Bili Copilot. All rights reserved.

using System.Net;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播播放失败的分类。
/// </summary>
public enum LiveFailureKind
{
    /// <summary>
    /// 没有失败.
    /// </summary>
    None,

    /// <summary>
    /// 请求被取消（关闭窗口或换房）.
    /// </summary>
    Cancelled,

    /// <summary>
    /// 播放地址不可用（房间不存在、下播、断流、网络错误）.
    /// </summary>
    AddressUnavailable,

    /// <summary>
    /// 鉴权失败（登录态无效或没有该清晰度的权限）.
    /// </summary>
    AuthenticationFailed,

    /// <summary>
    /// 解码/格式失败.
    /// </summary>
    DecodeFailed,

    /// <summary>
    /// 其他未分类失败.
    /// </summary>
    Unknown,
}

/// <summary>
/// 把取流阶段抛出的异常翻译成可读的失败分类。
/// </summary>
/// <remarks>
/// 解码失败来自播放器内核的错误事件，不走这里；这里只区分请求地址与鉴权。
/// 取址阶段返回 412（PreconditionFailed）通常意味着登录态/清晰度权限被拒绝，
/// 因此归为鉴权失败；播放阶段看到 412 时地址已经发出去，按传输失败换候选即可
/// （见 <see cref="LivePlaybackFailureClassifier"/>）。
/// </remarks>
public static class LiveFailureClassifier
{
    /// <summary>
    /// 判定失败分类。
    /// </summary>
    /// <param name="exception">取流或初始化时抛出的异常.</param>
    /// <returns>失败分类.</returns>
    public static LiveFailureKind Classify(Exception? exception)
        => exception switch
        {
            null => LiveFailureKind.None,
            OperationCanceledException => LiveFailureKind.Cancelled,
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.PreconditionFailed }
                => LiveFailureKind.AuthenticationFailed,
            HttpRequestException => LiveFailureKind.AddressUnavailable,
            System.Security.Authentication.AuthenticationException => LiveFailureKind.AuthenticationFailed,
            IOException or System.Net.Sockets.SocketException => LiveFailureKind.AddressUnavailable,
            _ => LiveFailureKind.Unknown,
        };
}

/// <summary>
/// 播放阶段（而非取址阶段）的失败分类；决定是换候选、重新取址还是交回手动重试。
/// </summary>
public enum LivePlaybackFailureKind
{
    /// <summary>
    /// 没有失败.
    /// </summary>
    None,

    /// <summary>
    /// 请求被取消（关闭窗口、换房或新的用户意图）.
    /// </summary>
    Cancelled,

    /// <summary>
    /// HTTP 403/404/412/5xx、连接失败或超时：换下一条地址.
    /// </summary>
    Transport,

    /// <summary>
    /// 解码器初始化/容器不支持：换下一条地址.
    /// </summary>
    DecoderInit,

    /// <summary>
    /// 无法归类：同样按可恢复失败处理.
    /// </summary>
    Unknown,
}

/// <summary>
/// 播放器内核错误在纯逻辑层的替身，避免直播策略依赖 MpvKernel。
/// </summary>
public enum LivePlayerError
{
    /// <summary>无错误.</summary>
    None,

    /// <summary>HTTP 403.</summary>
    Http403,

    /// <summary>HTTP 404.</summary>
    Http404,

    /// <summary>HTTP 412（签名地址被拒绝，取址阶段的同名状态归为鉴权失败）.</summary>
    Http412,

    /// <summary>HTTP 5xx.</summary>
    HttpServerError,

    /// <summary>TLS 失败.</summary>
    Tls,

    /// <summary>加载失败.</summary>
    LoadingFailed,

    /// <summary>视频输出/解码器初始化失败.</summary>
    VideoInitFailed,

    /// <summary>音频输出初始化失败.</summary>
    AudioInitFailed,

    /// <summary>不支持的格式.</summary>
    Unsupported,

    /// <summary>未知格式.</summary>
    UnknownFormat,

    /// <summary>没有可播放内容.</summary>
    NothingToPlay,

    /// <summary>其他错误.</summary>
    Other,
}

/// <summary>
/// 失败分类与恢复次序。
/// </summary>
/// <remarks>
/// 播放器内核在任意 HTTP 错误之后还会无条件再发一次 <c>VoInitFailed</c>（见 #199 调查），
/// 所以同一次播放尝试里只认第一条失败；后续错误视为回声忽略，避免一次失败跳掉两个候选。
/// </remarks>
public static class LivePlaybackFailureClassifier
{
    /// <summary>
    /// 把播放器内核错误映射为播放失败分类。
    /// </summary>
    /// <param name="error">播放器错误替身.</param>
    /// <returns>失败分类.</returns>
    public static LivePlaybackFailureKind FromPlayerError(LivePlayerError error)
        => error switch
        {
            LivePlayerError.None => LivePlaybackFailureKind.None,
            LivePlayerError.Http403
                or LivePlayerError.Http404
                or LivePlayerError.Http412
                or LivePlayerError.HttpServerError
                or LivePlayerError.Tls
                or LivePlayerError.LoadingFailed => LivePlaybackFailureKind.Transport,
            LivePlayerError.VideoInitFailed
                or LivePlayerError.AudioInitFailed
                or LivePlayerError.Unsupported
                or LivePlayerError.UnknownFormat
                or LivePlayerError.NothingToPlay => LivePlaybackFailureKind.DecoderInit,
            _ => LivePlaybackFailureKind.Unknown,
        };

    /// <summary>
    /// 把取址/重开异常映射为播放失败分类。
    /// </summary>
    /// <param name="exception">异常.</param>
    /// <returns>失败分类.</returns>
    public static LivePlaybackFailureKind ClassifyPlayback(Exception? exception)
        => exception switch
        {
            null => LivePlaybackFailureKind.None,
            OperationCanceledException { InnerException: TimeoutException } => LivePlaybackFailureKind.Transport,
            OperationCanceledException => LivePlaybackFailureKind.Cancelled,
            TimeoutException => LivePlaybackFailureKind.Transport,
            HttpRequestException { StatusCode: System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.InternalServerError or System.Net.HttpStatusCode.BadGateway or System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.GatewayTimeout } => LivePlaybackFailureKind.Transport,
            HttpRequestException => LivePlaybackFailureKind.Transport,
            System.Net.Sockets.SocketException => LivePlaybackFailureKind.Transport,
            IOException => LivePlaybackFailureKind.Transport,
            _ => LivePlaybackFailureKind.Unknown,
        };

    /// <summary>
    /// 把播放失败分类转成对用户展示的直播失败分类。
    /// </summary>
    /// <param name="kind">播放失败分类.</param>
    /// <returns>展示用失败分类.</returns>
    public static LiveFailureKind ToLiveFailureKind(LivePlaybackFailureKind kind)
        => kind switch
        {
            LivePlaybackFailureKind.Cancelled => LiveFailureKind.Cancelled,
            LivePlaybackFailureKind.Transport => LiveFailureKind.AddressUnavailable,
            LivePlaybackFailureKind.DecoderInit => LiveFailureKind.DecodeFailed,
            LivePlaybackFailureKind.Unknown => LiveFailureKind.Unknown,
            _ => LiveFailureKind.None,
        };
}

/// <summary>
/// 播放失败后的恢复动作。
/// </summary>
public enum LiveRecoveryAction
{
    /// <summary>
    /// 不动作（回声/取消/重复）.
    /// </summary>
    None,

    /// <summary>
    /// 换同一候选内的下一条地址，或下一个候选.
    /// </summary>
    AdvanceCandidate,

    /// <summary>
    /// 候选耗尽：重新请求 playurl（签名地址会过期）.
    /// </summary>
    ReRequestPlayUrl,

    /// <summary>
    /// 重新取址后仍失败：展示可读错误并等待手动重试.
    /// </summary>
    ManualRetry,
}

/// <summary>
/// 恢复次序：换候选 → 重新请求地址 → 手动重试。
/// </summary>
/// <param name="maxPlayUrlRequests">在同一轮会话中最多允许的重新取址次数.</param>
public sealed class LiveRecoverySequencer(int maxPlayUrlRequests = 1)
{
    private bool _failedInAttempt;
    private int _playUrlRequests;

    /// <summary>
    /// Gets 已发起的重新取址次数.
    /// </summary>
    public int PlayUrlRequests => _playUrlRequests;

    /// <summary>
    /// 记录一次失败并给出下一步动作。
    /// </summary>
    /// <param name="kind">失败分类.</param>
    /// <param name="hasNextCandidate">是否还有未尝试的地址.</param>
    /// <returns>恢复动作.</returns>
    public LiveRecoveryAction Report(LivePlaybackFailureKind kind, bool hasNextCandidate)
    {
        if (kind is LivePlaybackFailureKind.None or LivePlaybackFailureKind.Cancelled)
        {
            return LiveRecoveryAction.None;
        }

        if (_failedInAttempt)
        {
            // 内核会把 HTTP 错误再放大成一次 VoInitFailed；同一次尝试只认第一条。
            return LiveRecoveryAction.None;
        }

        _failedInAttempt = true;
        if (hasNextCandidate)
        {
            return LiveRecoveryAction.AdvanceCandidate;
        }

        if (_playUrlRequests < maxPlayUrlRequests)
        {
            _playUrlRequests++;
            return LiveRecoveryAction.ReRequestPlayUrl;
        }

        return LiveRecoveryAction.ManualRetry;
    }

    /// <summary>
    /// 开始一次新的播放尝试（加载了新的地址）.
    /// </summary>
    public void BeginAttempt() => _failedInAttempt = false;

    /// <summary>
    /// 复位整轮会话.
    /// </summary>
    public void Reset()
    {
        _failedInAttempt = false;
        _playUrlRequests = 0;
    }
}

/// <summary>
/// 候选与重新取址都失败后抛出的终态错误；界面据此展示可读提示并允许手动重试。
/// </summary>
public sealed class LivePlaybackUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LivePlaybackUnavailableException"/> class.
    /// </summary>
    public LivePlaybackUnavailableException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LivePlaybackUnavailableException"/> class.
    /// </summary>
    /// <param name="message">可读信息.</param>
    public LivePlaybackUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LivePlaybackUnavailableException"/> class.
    /// </summary>
    /// <param name="message">可读信息.</param>
    /// <param name="innerException">内部异常.</param>
    public LivePlaybackUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LivePlaybackUnavailableException"/> class.
    /// </summary>
    /// <param name="kind">失败分类.</param>
    /// <param name="message">可读信息.</param>
    public LivePlaybackUnavailableException(LivePlaybackFailureKind kind, string message)
        : base(message) => Kind = kind;

    /// <summary>
    /// Gets 失败分类.
    /// </summary>
    public LivePlaybackFailureKind Kind { get; }
}
