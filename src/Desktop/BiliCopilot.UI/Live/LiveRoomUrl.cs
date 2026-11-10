// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 直播间网页地址的唯一构造点。
/// </summary>
/// <remarks>
/// 卡片、连接器与播放失败兜底都打开同一个直播间网页，地址格式只在这里定义一次。
/// 房间号不写入设置、也不缓存任何签名播放地址（#192）。
/// </remarks>
public static class LiveRoomUrl
{
    private const string Prefix = "https://live.bilibili.com/";

    /// <summary>
    /// 构造直播间网页地址。
    /// </summary>
    /// <param name="roomId">直播间短号或真实房间号.</param>
    /// <returns>直播间网页地址；房间号为空时返回 <c>null</c>.</returns>
    public static Uri? Create(string? roomId)
        => string.IsNullOrWhiteSpace(roomId) ? null : new Uri(Prefix + roomId.Trim());

    /// <summary>
    /// 构造直播间网页地址文本。
    /// </summary>
    /// <param name="roomId">直播间短号或真实房间号.</param>
    /// <returns>直播间网页地址；房间号为空时为空字符串.</returns>
    public static string CreateString(string? roomId)
        => Create(roomId)?.AbsoluteUri ?? string.Empty;
}
