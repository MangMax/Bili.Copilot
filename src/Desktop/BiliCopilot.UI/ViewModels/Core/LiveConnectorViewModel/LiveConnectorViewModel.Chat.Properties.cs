// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 直播连接器的聊天侧栏状态（#197）：有界消息列表、屏蔽计数与折叠状态。
/// </summary>
/// <remarks>
/// 这些属性单独放在本 partial 文件里，避免与 #198 的房间状态属性互相覆盖。
/// 列表只在批量裁剪时整表重置，其余时候追加一条，不会每条消息重建整表。
/// </remarks>
public sealed partial class LiveConnectorViewModel
{
    /// <summary>
    /// Gets 聊天列表（有界，约 500 条）.
    /// </summary>
    public LiveChatItemCollection<LiveChatMessageItem> ChatMessages { get; } = [];

    /// <summary>
    /// Gets 屏蔽计数文案（按用户 ID / 昵称 / 关键词）.
    /// </summary>
    [ObservableProperty]
    public partial string? ChatBlockCountersText { get; set; }

    /// <summary>
    /// Gets 聊天列表是否为空.
    /// </summary>
    [ObservableProperty]
    public partial bool IsChatEmpty { get; set; } = true;

    /// <summary>
    /// Gets 是否正在补屏（进房拉取最近弹幕）.
    /// </summary>
    [ObservableProperty]
    public partial bool IsChatBackfilling { get; set; }

}
