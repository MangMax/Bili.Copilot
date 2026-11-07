// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播间的播出状态。
/// </summary>
public enum LiveRoomPhase
{
    /// <summary>
    /// 未知：房间详情还没到.
    /// </summary>
    Unknown,

    /// <summary>
    /// 正在直播.
    /// </summary>
    Living,

    /// <summary>
    /// 主播已下播（服务端未开播事件）.
    /// </summary>
    NotStarted,

    /// <summary>
    /// 直播被切断（服务端切断事件）.
    /// </summary>
    CutOff,
}

/// <summary>
/// 直播间实时状态的纯状态机：把内核房间事件归并成"在播 / 已下播 / 被切断"，
/// 并对播放地址重载做去重与房间归属判断。
/// </summary>
/// <remarks>
/// 下播与被切断是权威终态：到达后播放器不再自动重连，也不应显示"播放已结束"。
/// 只有新的 <see cref="ApplyStarted"/> 才能把状态恢复到在播。
/// 所有入口都带房间标识；不属于当前房间的事件（换房后迟到的旧事件）直接忽略。
/// 该类不依赖 WinUI，时间由调用方传入，便于用假时钟测试。
/// </remarks>
public sealed class LiveRoomStatusTracker
{
    /// <summary>
    /// 默认的播放地址重载冷却：同一房间内的连续重载请求在此窗口内只生效一次。
    /// </summary>
    public static readonly TimeSpan DefaultReloadCooldown = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _reloadCooldown;
    private string? _roomId;
    private DateTimeOffset? _lastReloadAt;
    private int _reloadCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveRoomStatusTracker"/> class.
    /// </summary>
    /// <param name="reloadCooldown">重载去重窗口.</param>
    public LiveRoomStatusTracker(TimeSpan? reloadCooldown = null)
        => _reloadCooldown = reloadCooldown ?? DefaultReloadCooldown;

    /// <summary>
    /// Gets 当前房间标识.
    /// </summary>
    public string? RoomId => _roomId;

    /// <summary>
    /// Gets 当前播出状态.
    /// </summary>
    public LiveRoomPhase Phase { get; private set; } = LiveRoomPhase.Unknown;

    /// <summary>
    /// Gets 是否处于终态（下播/被切断）.
    /// </summary>
    public bool IsTerminal => Phase is LiveRoomPhase.NotStarted or LiveRoomPhase.CutOff;

    /// <summary>
    /// Gets 当前展示的在线人数；尚未收到事件时为空.
    /// </summary>
    public int? OnlineCount { get; private set; }

    /// <summary>
    /// Gets 在线人数的数据来源.
    /// </summary>
    public LiveOnlineCountSource OnlineCountSource { get; private set; }

    /// <summary>
    /// Gets 已经生效的播放地址重载次数.
    /// </summary>
    public int ReloadCount => _reloadCount;

    /// <summary>
    /// 绑定当前房间；换房会清空人数、终态与重载去重窗口。
    /// </summary>
    /// <param name="roomId">直播间标识.</param>
    public void SetRoom(string? roomId)
    {
        if (string.Equals(_roomId, roomId, StringComparison.Ordinal))
        {
            return;
        }

        _roomId = roomId;
        Phase = LiveRoomPhase.Unknown;
        OnlineCount = default;
        OnlineCountSource = default;
        _lastReloadAt = default;
        _reloadCount = 0;
    }

    /// <summary>
    /// 用房间详情初始化状态。
    /// </summary>
    /// <param name="isLiving">详情中的在播标记.</param>
    public void ApplyRoomDetail(bool isLiving)
        => Phase = isLiving ? LiveRoomPhase.Living : LiveRoomPhase.NotStarted;

    /// <summary>
    /// 主播开播：从任意状态恢复到在播。
    /// </summary>
    /// <param name="roomId">事件所属房间.</param>
    /// <returns>状态是否发生变化.</returns>
    public bool ApplyStarted(string? roomId)
    {
        if (!IsCurrentRoom(roomId))
        {
            return false;
        }

        var changed = Phase != LiveRoomPhase.Living;
        Phase = LiveRoomPhase.Living;
        return changed;
    }

    /// <summary>
    /// 主播已下播。
    /// </summary>
    /// <param name="roomId">事件所属房间.</param>
    /// <returns>状态是否发生变化.</returns>
    public bool ApplyNotStarted(string? roomId)
    {
        if (!IsCurrentRoom(roomId))
        {
            return false;
        }

        var changed = Phase != LiveRoomPhase.NotStarted;
        Phase = LiveRoomPhase.NotStarted;
        return changed;
    }

    /// <summary>
    /// 直播被切断。
    /// </summary>
    /// <param name="roomId">事件所属房间.</param>
    /// <returns>状态是否发生变化.</returns>
    public bool ApplyCutOff(string? roomId)
    {
        if (!IsCurrentRoom(roomId))
        {
            return false;
        }

        var changed = Phase != LiveRoomPhase.CutOff;
        Phase = LiveRoomPhase.CutOff;
        return changed;
    }

    /// <summary>
    /// 在线人数变化：看过人数（Watched）优先于人气值（Popularity）。
    /// </summary>
    /// <param name="roomId">事件所属房间.</param>
    /// <param name="count">人数或人气值.</param>
    /// <param name="source">数据来源.</param>
    /// <returns>展示值是否发生变化.</returns>
    public bool ApplyOnlineCount(string? roomId, int count, LiveOnlineCountSource source)
    {
        if (!IsCurrentRoom(roomId) || count < 0)
        {
            return false;
        }

        if (OnlineCount == count && OnlineCountSource == source)
        {
            return false;
        }

        if (source == LiveOnlineCountSource.Popularity && OnlineCountSource == LiveOnlineCountSource.Watched)
        {
            // 已经拿到真实看过人数后，人气值不再覆盖展示值。
            return false;
        }

        OnlineCount = count;
        OnlineCountSource = source;
        return true;
    }

    /// <summary>
    /// 判断一次播放地址重载请求是否应当放行：终态不放行，冷却窗口内不放行，换房后重新计时。
    /// </summary>
    /// <param name="roomId">事件所属房间.</param>
    /// <param name="now">当前时间.</param>
    /// <returns>需要刷新时为 true.</returns>
    public bool TryBeginReload(string? roomId, DateTimeOffset now)
    {
        if (!IsCurrentRoom(roomId) || IsTerminal)
        {
            return false;
        }

        if (_lastReloadAt is { } last && now - last < _reloadCooldown && now >= last)
        {
            return false;
        }

        _lastReloadAt = now;
        _reloadCount++;
        return true;
    }

    private bool IsCurrentRoom(string? roomId)
        => !string.IsNullOrEmpty(roomId)
            && (string.IsNullOrEmpty(_roomId) || string.Equals(_roomId, roomId, StringComparison.Ordinal));
}
