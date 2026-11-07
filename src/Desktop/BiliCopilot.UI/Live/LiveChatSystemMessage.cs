// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.Live;

/// <summary>
/// 系统消息映射：把连接状态与房间生命周期事件翻译成聊天列表里的系统提示。
/// </summary>
/// <remarks>
/// 只做映射，不发请求、不改房间状态；#198 的房间状态 UI 使用同一批内核事件。
/// </remarks>
public static class LiveChatSystemMessage
{
    /// <summary>
    /// 由连接状态创建系统消息。
    /// </summary>
    /// <param name="state">连接状态.</param>
    /// <param name="detail">服务端说明.</param>
    /// <param name="sequence">会话内序号.</param>
    /// <param name="timestamp">发生时间.</param>
    /// <returns>系统消息.</returns>
    public static LiveChatMessage FromConnectionState(
        LiveDanmakuConnectionState state,
        string? detail,
        long sequence,
        DateTimeOffset timestamp)
        => LiveChatMessage.System(GetSystemKind(state), sequence, timestamp, detail);

    /// <summary>
    /// 由认证失败创建系统消息；认证失败不重试，因此单独成一类。
    /// </summary>
    /// <param name="detail">失败说明.</param>
    /// <param name="sequence">会话内序号.</param>
    /// <param name="timestamp">发生时间.</param>
    /// <returns>系统消息.</returns>
    public static LiveChatMessage FromAuthenticationFailure(string? detail, long sequence, DateTimeOffset timestamp)
        => LiveChatMessage.System(LiveChatSystemKind.AuthenticationFailed, sequence, timestamp, detail);

    /// <summary>
    /// 由内核事件创建系统消息；非系统事件返回空。
    /// </summary>
    /// <param name="liveEvent">内核事件.</param>
    /// <param name="sequence">会话内序号.</param>
    /// <param name="timestamp">发生时间.</param>
    /// <returns>系统消息；事件不需要系统提示时为 null.</returns>
    public static LiveChatMessage? FromEvent(LiveDanmakuEvent? liveEvent, long sequence, DateTimeOffset timestamp)
        => liveEvent switch
        {
            LiveRoomNotStartedEvent => LiveChatMessage.System(LiveChatSystemKind.RoomNotStarted, sequence, timestamp),
            LiveStartedEvent => LiveChatMessage.System(LiveChatSystemKind.RoomStarted, sequence, timestamp),
            LiveRoomCutOffEvent cutOff => LiveChatMessage.System(LiveChatSystemKind.RoomCutOff, sequence, timestamp, cutOff.Message),
            _ => null,
        };

    /// <summary>
    /// 连接状态到系统消息分类。
    /// </summary>
    /// <param name="state">连接状态.</param>
    /// <returns>系统消息分类.</returns>
    public static LiveChatSystemKind GetSystemKind(LiveDanmakuConnectionState state)
        => state switch
        {
            LiveDanmakuConnectionState.Connecting => LiveChatSystemKind.Connecting,
            LiveDanmakuConnectionState.Connected => LiveChatSystemKind.Connected,
            LiveDanmakuConnectionState.Reconnecting => LiveChatSystemKind.Reconnecting,
            _ => LiveChatSystemKind.Disconnected,
        };
}
