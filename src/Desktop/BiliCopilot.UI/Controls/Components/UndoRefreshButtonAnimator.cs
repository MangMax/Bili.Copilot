// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Media.Animation;
using System.Runtime.CompilerServices;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 「撤销刷新」按钮的出现与消失.
/// </summary>
/// <remarks>
/// 只让撤销按钮自己动：按钮组右对齐，撤销折叠时刷新按钮在屏幕上的位置并不变化
/// （实测有/无撤销时刷新按钮都在同一 x），所以既不需要补位动画，也不能给容器套重排过渡——
/// 一旦套上，撤销折叠时刷新按钮相对父级的偏移变化会被 <c>RepositionThemeTransition</c> 捕捉到，
/// 刷新按钮就会跟着动。出现一侧的缩放由 XAML 里的 <c>Implicit.ShowAnimations</c> 承担；
/// 消失一侧必须在淡出结束之后再折叠，因为 <c>Collapsed</c> 会跳过隐式隐藏动画。
/// </remarks>
internal static class UndoRefreshButtonAnimator
{
    private const string OpacityPath = "Opacity";
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(160);

    /// <summary>
    /// 每个按钮当前正在播放的淡出，用于中途被打断时及时释放.
    /// </summary>
    private static readonly ConditionalWeakTable<FrameworkElement, Storyboard> RunningFades = new();

    /// <summary>
    /// 按当前状态摆放撤销按钮，可选择是否播放消失动画.
    /// </summary>
    /// <param name="button">撤销按钮.</param>
    /// <param name="shouldShow">是否应当显示.</param>
    /// <param name="animate">是否播放动画.</param>
    /// <param name="isStillHidden">淡出结束前再次确认「不应显示」.</param>
    public static void Apply(
        FrameworkElement button,
        bool shouldShow,
        bool animate,
        Func<bool>? isStillHidden = null)
    {
        if (shouldShow)
        {
            // 淡出途中又需要显示时先停掉它，否则会先淡到 0 再跳回 1。
            StopRunningFade(button);
            button.Opacity = 1;
            button.Visibility = Visibility.Visible;
            return;
        }

        if (!animate || button.Visibility != Visibility.Visible)
        {
            StopRunningFade(button);
            Hide(button);
            return;
        }

        StopRunningFade(button);

        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = FadeOutDuration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },

            // 结束后释放动画，否则 HoldEnd 会把 Opacity 长期钉在 0，
            // 下一次显示时基础值再设回 1 也不生效，按钮会一直不可见。
            FillBehavior = FillBehavior.Stop,
        };
        Storyboard.SetTarget(animation, button);
        Storyboard.SetTargetProperty(animation, OpacityPath);

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            RunningFades.Remove(button);

            // 淡出期间可能又刷新成功，撤销入口重新需要显示，此时不能再隐藏。
            if (isStillHidden?.Invoke() is not false)
            {
                Hide(button);
            }
        };

        RunningFades.AddOrUpdate(button, storyboard);
        storyboard.Begin();
    }

    private static void StopRunningFade(FrameworkElement button)
    {
        if (RunningFades.TryGetValue(button, out var storyboard))
        {
            storyboard.Stop();
            RunningFades.Remove(button);
        }
    }

    private static void Hide(FrameworkElement button)
    {
        button.Visibility = Visibility.Collapsed;
        button.Opacity = 1;
    }
}
