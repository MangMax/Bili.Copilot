// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 用户空间分区类型.
/// </summary>
public enum UserSpaceTabKind
{
    /// <summary>
    /// 主页：最近投稿与最近动态.
    /// </summary>
    Home,

    /// <summary>
    /// 动态.
    /// </summary>
    Moments,

    /// <summary>
    /// 投稿.
    /// </summary>
    Submission,

    /// <summary>
    /// 投稿下的视频.
    /// </summary>
    Videos,

    /// <summary>
    /// 投稿下的专栏.
    /// </summary>
    Articles,

    /// <summary>
    /// 投稿下的图文动态.
    /// </summary>
    MomentPosts,
}

/// <summary>
/// 用户空间里的一个分区.
/// </summary>
/// <param name="title">分区标题.</param>
/// <param name="kind">分区类型.</param>
public sealed class UserSpaceTabViewModel(string title, UserSpaceTabKind kind)
{
    /// <summary>
    /// 分区标题.
    /// </summary>
    public string Title { get; } = title;

    /// <summary>
    /// 分区类型.
    /// </summary>
    public UserSpaceTabKind Kind { get; } = kind;
}
