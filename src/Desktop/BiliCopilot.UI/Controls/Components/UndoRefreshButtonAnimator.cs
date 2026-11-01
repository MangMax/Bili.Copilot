// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.Runtime.CompilerServices;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 「撤销刷新」按钮的出现与消失.
/// </summary>
/// <remarks>
/// 按钮的出现和消失只由这里驱动，XAML 上不再挂 <c>Implicit.ShowAnimations</c>：
/// 组合层的隐式动画和这里的 <see cref="Storyboard"/> 会同时写 <c>Opacity</c>，
/// 淡出释放动画时按钮又被拉回基础值，列表更新占住 UI 线程时就表现为闪烁。
/// 淡出使用 <c>FillBehavior.HoldEnd</c> 把呈现值停在 0，完成回调再折叠；
/// 即使回调被列表更新推迟，动画结束后的每一帧也都还是 0，不会整块亮回 1。
/// 释放动画前先把基础值写成目标值，因此 Stop/卸载不会先渲染一帧旧值；
/// 折叠本身不需要任何延时。
/// 重复的「不应显示」通知不会重启淡出；淡入途中收到隐藏通知时等淡入结束再淡出，
/// 避免从中间不透明度直接跳到 1。
/// </remarks>
internal static class UndoRefreshButtonAnimator
{
    private const string OpacityPath = "Opacity";
    private const string ScaleXPath = "ScaleX";
    private const string ScaleYPath = "ScaleY";

    private static readonly TimeSpan ShowFadeDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan ShowScaleDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan HideFadeDuration = TimeSpan.FromMilliseconds(160);

    /// <summary>
    /// 每个按钮当前正在播放的过渡，用于中途被打断时及时释放.
    /// </summary>
    private static readonly ConditionalWeakTable<FrameworkElement, RunningTransition> Running = new();

    /// <summary>
    /// 按当前状态摆放撤销按钮，可选择是否播放动画.
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
        // A hidden subtree may never deliver Completed: settle it before considering an existing transition.
        if (!animate || (!shouldShow && (button.Visibility != Visibility.Visible || !IsEffectivelyVisible(button))))
        {
            Stop(button, opacity: 1);
            button.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
            ResetScale(button);
            return;
        }

        var running = Running.TryGetValue(button, out var current) ? current : null;

        if (shouldShow)
        {
            if (running is not null)
            {
                if (!running.Hiding)
                {
                    // 淡入已经由当前过渡负责，重复通知不再重启。
                    running.HideAfterShow = false;
                    return;
                }

                // 淡出途中又需要显示：先停在完全可见，再按需淡入。
                Stop(button, opacity: 1);
            }
            else if (button.Visibility == Visibility.Visible && button.Opacity >= 1)
            {
                return;
            }

            Show(button, animate);
            return;
        }

        if (running is not null)
        {
            if (running.Hiding)
            {
                // 同一段淡出只发起一次。
                return;
            }

            // 淡入还没结束：让它停到 1 之后再淡出，避免中间值跳变。
            running.HideAfterShow = true;
            running.IsStillHidden = isStillHidden;
            return;
        }

        PlayHide(button, isStillHidden);
    }

    /// <summary>
    /// 放弃进行中的过渡，供头部卸载时调用.
    /// </summary>
    /// <remarks>
    /// 只停止动画并把基础值摆到过渡的终态，不按「当前可见」重新显示按钮；
    /// 重新挂载时由 <see cref="Apply"/> 按视图模型状态重新摆放。
    /// </remarks>
    /// <param name="button">撤销按钮.</param>
    public static void Cancel(FrameworkElement button)
    {
        var opacity = Running.TryGetValue(button, out var transition) && transition.Hiding ? 0 : 1;
        Stop(button, opacity);
        ResetScale(button);
    }

    private static void Show(FrameworkElement button, bool animate)
    {
        if (!animate)
        {
            button.Visibility = Visibility.Visible;
            button.Opacity = 1;
            ResetScale(button);
            return;
        }

        // 显示前先把基础值设为 0，避免从折叠切到可见的那一帧先亮到 1。
        button.Opacity = 0;
        ResetScale(button);
        button.Visibility = Visibility.Visible;

        var storyboard = new Storyboard();
        AddDoubleAnimation(storyboard, button, OpacityPath, 0, 1, ShowFadeDuration);
        if (button.RenderTransform is CompositeTransform transform)
        {
            AddDoubleAnimation(storyboard, transform, ScaleXPath, 0.96, 1, ShowScaleDuration);
            AddDoubleAnimation(storyboard, transform, ScaleYPath, 0.96, 1, ShowScaleDuration);
        }

        var transition = new RunningTransition(storyboard);
        storyboard.Completed += (_, _) =>
        {
            // 被打断的旧时间线可能仍在这一帧派发完成回调，只处理仍是当前那一段的过渡。
            if (!Take(button, transition))
            {
                return;
            }

            // 先把基础值写成终态，再释放 HoldEnd，避免下一次显示被旧动画钉住。
            button.Opacity = 1;
            ResetScale(button);
            storyboard.Stop();

            if (transition.HideAfterShow)
            {
                PlayHide(button, transition.IsStillHidden);
            }
        };

        Running.AddOrUpdate(button, transition);
        storyboard.Begin();
    }

    private static void PlayHide(FrameworkElement button, Func<bool>? isStillHidden)
    {
        var storyboard = new Storyboard();
        AddDoubleAnimation(storyboard, button, OpacityPath, 1, 0, HideFadeDuration);

        var transition = new RunningTransition(storyboard, hiding: true);
        storyboard.Completed += (_, _) =>
        {
            if (!Running.TryGetValue(button, out var current) || !ReferenceEquals(current, transition))
            {
                return;
            }

            // The predicate can synchronously replace/cancel this transition.
            var stillHidden = isStillHidden?.Invoke() is not false;
            if (!Take(button, transition))
            {
                storyboard.Stop();
                return;
            }

            if (!stillHidden)
            {
                // Take removed the registration, so release the captured clock explicitly.
                button.Opacity = 1;
                storyboard.Stop();
                ResetScale(button);
                Show(button, animate: IsEffectivelyVisible(button));
                return;
            }

            // HoldEnd 已经把呈现值停在 0；这里折叠并释放，折叠后写回基础值。
            button.Visibility = Visibility.Collapsed;
            button.Opacity = 1;
            ResetScale(button);
            storyboard.Stop();
        };

        Running.AddOrUpdate(button, transition);
        storyboard.Begin();
    }

    private static void AddDoubleAnimation(
        Storyboard storyboard,
        DependencyObject target,
        string propertyPath,
        double from,
        double to,
        TimeSpan duration)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = duration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd,
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, propertyPath);
        storyboard.Children.Add(animation);
    }

    /// <summary>
    /// 停止当前过渡，先写基础值再释放动画，避免释放时先渲染一帧旧值.
    /// </summary>
    private static void Stop(FrameworkElement button, double opacity)
    {
        button.Opacity = opacity;
        if (Running.TryGetValue(button, out var transition))
        {
            Running.Remove(button);
            transition.Storyboard.Stop();
        }
    }

    /// <summary>
    /// 判断按钮当前是否真的会被渲染.
    /// </summary>
    /// <remarks>
    /// 设置页与流行页/分区页共用同一个窗口，切换页面只是把页面容器设为 Collapsed。
    /// 在隐藏的子树里启动淡出，动画不保证推进到 <c>Completed</c>，撤销按钮就会卡在 Visible；
    /// 因此不可见时直接折叠，等页面重新显示时状态已经一致。
    /// </remarks>
    private static bool IsEffectivelyVisible(DependencyObject element)
    {
        if (element is not FrameworkElement root || !root.IsLoaded)
        {
            return false;
        }

        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement ui && ui.Visibility == Visibility.Collapsed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Take(FrameworkElement button, RunningTransition transition)
    {
        if (!Running.TryGetValue(button, out var current) || !ReferenceEquals(current, transition))
        {
            return false;
        }

        Running.Remove(button);
        return true;
    }

    private static void ResetScale(FrameworkElement button)
    {
        if (button.RenderTransform is CompositeTransform transform)
        {
            transform.ScaleX = 1;
            transform.ScaleY = 1;
        }
    }

    private sealed class RunningTransition(Storyboard storyboard, bool hiding = false)
    {
        public Storyboard Storyboard { get; } = storyboard;

        public bool Hiding { get; } = hiding;

        public bool HideAfterShow { get; set; }

        public Func<bool>? IsStillHidden { get; set; }
    }
}
