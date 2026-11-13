// Copyright (c) Bili Copilot. All rights reserved.

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
        if (!AvatarTransitionPolicy.CanPrepare(
                animationsEnabled,
                sourceVisible,
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
    /// <param name="waitForImage">等待头像解码完成的回调，参数为剩余预算.</param>
    /// <returns>动画已启动时为 <see langword="true"/>；否则已清理状态.</returns>
    public static async Task<bool> TryStartAsync(
        UIElement destination,
        string userId,
        Uri avatarUri,
        Func<TimeSpan, Task<bool>> waitForImage)
    {
        var request = AvatarTransitionSession.Current;
        var age = request is null ? TimeSpan.Zero : DateTimeOffset.Now - request.PreparedAt;
        if (request is null || !AvatarTransitionPolicy.CanStart(request, userId, avatarUri, age))
        {
            Cancel();
            return false;
        }

        var ready = false;
        try
        {
            ready = await waitForImage(AvatarTransitionPolicy.GetImageWaitBudget(request, age));
        }
        catch (Exception ex)
        {
            // 等待失败与超时等价：取消动画，按普通页面切换处理.
            System.Diagnostics.Debug.WriteLine(ex);
            ready = false;
        }

        // 等待期间可能发生新的点击并替换了请求：旧的等待结果必须作废，
        // 且不能取消最后一次点击准备的动画。
        var stillCurrent = ReferenceEquals(AvatarTransitionSession.Current, request);
        if (!stillCurrent)
        {
            return false;
        }

        if (!ready
            || !AvatarTransitionPolicy.CanStart(
                request, userId, avatarUri, DateTimeOffset.Now - request.PreparedAt))
        {
            Cancel();
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
