// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 视频卡片的骨架占位.
/// </summary>
/// <remarks>
/// 只表达「这里将有一张卡片」，不进入任何业务列表，也不参与刷新快照与差异计算；
/// 它渲染在真实卡片之下，真实卡片到达后自然把它盖住。
/// </remarks>
public sealed class VideoCardSkeleton
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VideoCardSkeleton"/> class.
    /// </summary>
    /// <param name="index">占位在骨架层中的序号，用于错开动画.</param>
    public VideoCardSkeleton(int index) => Index = index;

    /// <summary>
    /// 占位序号.
    /// </summary>
    public int Index { get; }
}
