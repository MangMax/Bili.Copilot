// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 推荐列表即将变化时，页面应如何处理当前视口.
/// </summary>
public enum RecommendationListChange
{
    /// <summary>
    /// 保持变化前第一张可见卡片在视口中的位置，用于非增量刷新。
    /// </summary>
    PreserveVisibleCard,

    /// <summary>
    /// 变化完成后回到列表顶部，用于成功的增量刷新。
    /// </summary>
    ResetToTop,

    /// <summary>
    /// 撤销刷新，回到刷新前的视口。
    /// </summary>
    UndoRefresh,
}
