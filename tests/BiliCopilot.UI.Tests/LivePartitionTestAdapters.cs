// Copyright (c) Bili Copilot. All rights reserved.

// Headless adapters only for the live card item and the Windows presentation surface.
// LivePartitionDetailViewModel, PartitionViewModel and ILiveDiscoveryService are production code.
using BiliCopilot.UI.Models.Constants;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Items
{
    /// <summary>
    /// 直播卡片条目的无 WinUI 替身：详情视图模型只构造它并透出原始数据。
    /// </summary>
    public sealed class LiveItemViewModel(LiveInformation data, LiveCardStyle style)
    {
        public LiveInformation Data { get; } = data;

        public LiveCardStyle Style { get; } = style;
    }
}
