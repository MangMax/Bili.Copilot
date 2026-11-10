// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>
/// 聊天列表的滚动所有权：自动跟随、用户回看与"回到底部"之间的状态迁移。
/// </summary>
/// <remarks>
/// 纯逻辑：只根据"谁在滚、滚到哪里"决定是否继续跟随，界面层照此决定要不要滚到底。
/// 用户一旦离开底部就停止跟随，此后新消息不会把用户拉回底部；
/// 只有用户自己点"回到底部"或重新滚到底部才恢复跟随。
/// </remarks>
public sealed class LiveChatScrollOwnership
{
    /// <summary>
    /// 默认的"贴底"判定阈值（像素）。
    /// </summary>
    public const double DefaultBottomThreshold = 24d;

    private readonly double _threshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatScrollOwnership"/> class.
    /// </summary>
    /// <param name="bottomThreshold">贴底判定阈值.</param>
    public LiveChatScrollOwnership(double bottomThreshold = DefaultBottomThreshold)
        => _threshold = bottomThreshold;

    /// <summary>
    /// Gets a value indicating whether 当前是否自动跟随新消息.
    /// </summary>
    public bool IsFollowing { get; private set; } = true;

    /// <summary>
    /// Gets a value indicating whether 是否显示"回到底部".
    /// </summary>
    public bool ShouldShowBackToBottom { get; private set; }

    /// <summary>
    /// 上报一次用户滚动；返回跟随状态是否发生变化。
    /// </summary>
    /// <param name="offset">当前偏移.</param>
    /// <param name="extent">内容高度.</param>
    /// <param name="viewport">可视高度.</param>
    /// <returns>跟随状态发生变化时为 true.</returns>
    public bool ReportUserScroll(double offset, double extent, double viewport)
    {
        var before = IsFollowing;
        var atBottom = IsAtBottom(offset, extent, viewport);
        IsFollowing = atBottom;
        ShouldShowBackToBottom = !atBottom;
        return before != IsFollowing;
    }

    /// <summary>
    /// 上报一次由自动跟随触发的滚动；不影响跟随状态。
    /// </summary>
    /// <param name="offset">当前偏移.</param>
    /// <param name="extent">内容高度.</param>
    /// <param name="viewport">可视高度.</param>
    public void ReportProgrammaticScroll(double offset, double extent, double viewport)
    {
        if (IsAtBottom(offset, extent, viewport))
        {
            ShouldShowBackToBottom = false;
        }
    }

    /// <summary>
    /// 上报一次新消息追加。
    /// </summary>
    /// <returns>是否需要滚到底部.</returns>
    public bool ReportAppend() => IsFollowing;

    /// <summary>Transfers ownership immediately when the user begins interacting.</summary>
    public void BeginUserInteraction()
    {
        IsFollowing = false;
        ShouldShowBackToBottom = true;
    }

    /// <summary>
    /// 用户点了"回到底部"。
    /// </summary>
    public void RequestBackToBottom()
    {
        IsFollowing = true;
        ShouldShowBackToBottom = false;
    }

    /// <summary>
    /// 换房或重开列表时复位。
    /// </summary>
    public void Reset()
    {
        IsFollowing = true;
        ShouldShowBackToBottom = false;
    }

    private bool IsAtBottom(double offset, double extent, double viewport)
    {
        var target = Math.Max(0d, extent - viewport);
        return target <= _threshold || Math.Abs(offset - target) < _threshold;
    }
}
