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
    /// Gets 聊天侧栏是否折叠；窄窗口下用户可收起.
    /// </summary>
    [ObservableProperty]
    public partial bool IsChatCollapsed { get; set; }

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

    /// <summary>
    /// Gets or sets a value indicating whether 竖屏下用户主动展开了聊天面板.
    /// </summary>
    [ObservableProperty]
    public partial bool IsChatExpandedInPortrait { get; set; }

    /// <summary>
    /// Gets a value indicating whether 是否应把聊天/状态面板收起为窄条。
    /// </summary>
    /// <remarks>
    /// 竖屏直播（#198）不能让侧栏盖住画面：窗口是竖屏且用户没有主动展开时，
    /// 面板只保留状态行与"展开聊天"入口，把宽度让给视频。
    /// 这里用窗口宽高比作为"竖屏"的代理——直播流本身的竖屏标记需要内核 API，
    /// 当前内核版本没有该字段，因此不做流级判定，也不去改动 mpv 的画面缩放。
    /// </remarks>
    public bool IsChatPanelCollapsed => IsChatCollapsed || (Parent.IsVerticalScreen && !IsChatExpandedInPortrait);

    /// <summary>
    /// 竖屏下切换聊天面板的展开/收起.
    /// </summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void TogglePortraitChat()
    {
        // 入口只在面板收起时显示：点击必须先把通用折叠位清掉，
        // 否则 IsChatCollapsed 仍为 true，展开看起来"没反应"。
        IsChatCollapsed = false;
        IsChatExpandedInPortrait = true;
        OnPropertyChanged(nameof(IsChatPanelCollapsed));
    }

    partial void OnIsChatCollapsedChanged(bool value) => OnPropertyChanged(nameof(IsChatPanelCollapsed));

    partial void OnIsChatExpandedInPortraitChanged(bool value) => OnPropertyChanged(nameof(IsChatPanelCollapsed));
}
