// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Richasy.BiliKernel.Models.User;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 把卡片头像串联到用户空间页头的 WinUI 适配器.
/// </summary>
/// <remarks>
/// 主窗口的叠层页宿主不是 Frame，因此只使用手动
/// <see cref="ConnectedAnimationService.PrepareToAnimate"/> /
/// <see cref="ConnectedAnimation.TryStart(UIElement)"/>.
/// 所有调用都必须可以安全失败：动画只是增强，不能阻塞导航.
/// </remarks>
public static class AvatarConnectedAnimation
{
    private const string AnimationKey = "user-space-avatar";

    /// <summary>
    /// 准备一次头像串联，同时取消之前尚未启动的动画.
    /// </summary>
    /// <param name="source">卡片上可见的头像元素.</param>
    /// <param name="user">卡片对应的作者资料.</param>
    /// <returns>成功准备时为 <see langword="true"/>.</returns>
    public static bool Prepare(FrameworkElement? source, UserProfile? user)
    {
        // 同一时刻只允许一个未完成的串联：先取消旧动画，避免旧快照悬停在界面上.
        Cancel();

        var avatarUri = user?.Avatar?.Uri;
        var animationsEnabled = false;
        try
        {
            animationsEnabled = new UISettings().AnimationsEnabled;
        }
        catch (Exception ex)
        {
            // 无法读取系统动画设置时按关闭处理，导航照常进行.
            System.Diagnostics.Debug.WriteLine(ex);
        }

        var sourceVisible = source is { IsLoaded: true, Visibility: Visibility.Visible }
            && source.ActualWidth > 0;

        // 卡片可能仍在显示作者首字占位；只有已经显示图片的头像才值得串联.
        var sourceHasImage = source is not RecommendationCoverImage cover || cover.Source is not null;
        if (!AvatarTransitionPolicy.CanPrepare(
                animationsEnabled,
                sourceVisible,
                sourceHasImage,
                user is not null,
                user?.Id,
                avatarUri))
        {
            return false;
        }

        try
        {
            var animation = ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate(AnimationKey, source!);
            animation.Configuration = new BasicConnectedAnimationConfiguration();
            AvatarTransitionSession.Prepare(user!.Id, avatarUri!, DateTimeOffset.Now);
            return true;
        }
        catch (Exception)
        {
            Cancel();
            return false;
        }
    }

    /// <summary>
    /// 目标页可见并完成布局后启动头像串联.
    /// </summary>
    /// <param name="destination">页头头像元素.</param>
    /// <param name="userId">目标页展示的用户 Id.</param>
    /// <param name="avatarUri">目标页使用的头像地址.</param>
    /// <returns>动画已启动时为 <see langword="true"/>；否则已清理状态.</returns>
    public static bool TryStart(
        UIElement destination,
        string userId,
        Uri avatarUri)
    {
        var request = AvatarTransitionSession.Current;
        if (request is null
            || !AvatarTransitionPolicy.CanStart(request, userId, avatarUri, DateTimeOffset.Now))
        {
            Cancel();
            return false;
        }

        // 目标元素尺寸固定，不等待头像解码：解码在动画背后完成，
        // 否则首次打开页面的布局耗时就会耗尽等待预算，动画永远不会出现.
        var current = AvatarTransitionSession.Current;
        if (!ReferenceEquals(current, request))
        {
            // 等待期间发生了新的点击：旧动画已由新的 Prepare 取消，
            // 这里不能误伤最后一次点击准备的动画.
            if (current is null)
            {
                Cancel();
            }

            return false;
        }

        try
        {
            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation(AnimationKey);
            if (animation is null || !animation.TryStart(destination))
            {
                Cancel();
                return false;
            }

            // 动画已交给系统播放，不再保留待启动状态.
            AvatarTransitionSession.Clear();
            return true;
        }
        catch (Exception)
        {
            Cancel();
            return false;
        }
    }

    /// <summary>
    /// 取消并释放尚未启动的动画.
    /// </summary>
    public static void Cancel()
    {
        AvatarTransitionSession.Clear();
        try
        {
            ConnectedAnimationService.GetForCurrentView().GetAnimation(AnimationKey)?.Cancel();
        }
        catch (Exception ex)
        {
            // 服务在窗口关闭或换根时可能不可用；取消本身不再抛出.
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
