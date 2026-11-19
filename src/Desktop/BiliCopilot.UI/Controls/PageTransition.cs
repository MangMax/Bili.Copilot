// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using System.Runtime.CompilerServices;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Controls;

/// <summary>
/// 页面、标签与侧栏切换的统一过渡.
/// </summary>
/// <remarks>
/// <para>
/// 三种场景使用不同动法：页面切换统一上浮；标签按标签栏顺序左右滑入；
/// 侧栏按列表顺序上下滑入。标签与侧栏的方向由选择控件发布，内容宿主只消费最近一次选择.
/// </para>
/// <para>
/// 三种触发方式：
/// <list type="bullet">
/// <item><c>PageTransition.Trigger</c>：绑定的内容键（选中分区、标签或视图模型）变化时播放.</item>
/// <item><c>PageTransition.AnimateOnShow</c>：元素由折叠变为可见时播放，用于靠 Visibility 切换的分区.</item>
/// <item><c>PageTransition.TrackSelection</c>：在选择控件上记录方向，供内容宿主的触发器消费.</item>
/// </list>
/// 首次数据绑定不播放；系统关闭动画时直接落到最终状态.
/// </para>
/// </remarks>
public static class PageTransition
{
    private static readonly ConditionalWeakTable<FrameworkElement, TransitionState> States = new();
    private static UISettings? _uiSettings;

    /// <summary>
    /// 内容键；变化时播放过渡.
    /// </summary>
    public static readonly DependencyProperty TriggerProperty = DependencyProperty.RegisterAttached(
        "Trigger",
        typeof(object),
        typeof(PageTransition),
        new PropertyMetadata(null, OnTriggerChanged));

    /// <summary>
    /// 元素由隐藏变为可见时播放过渡.
    /// </summary>
    public static readonly DependencyProperty AnimateOnShowProperty = DependencyProperty.RegisterAttached(
        "AnimateOnShow",
        typeof(bool),
        typeof(PageTransition),
        new PropertyMetadata(false, OnAnimateOnShowChanged));

    /// <summary>
    /// 宿主所属的切换场景；默认跟随最近一次选择自动判断.
    /// </summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.RegisterAttached(
        "Kind",
        typeof(PageTransitionKind),
        typeof(PageTransition),
        new PropertyMetadata(PageTransitionKind.Auto));

    /// <summary>
    /// 真正滑动的内容元素；宿主本身只负责淡入并保持外框不动，和动态页的分区切换一致.
    /// </summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.RegisterAttached(
        "Content",
        typeof(FrameworkElement),
        typeof(PageTransition),
        new PropertyMetadata(null));

    /// <summary>
    /// 在选择控件上记录用户选择的方向（标签栏由 <c>SlidingTabBar</c> 自行发布）.
    /// </summary>
    public static readonly DependencyProperty TrackSelectionProperty = DependencyProperty.RegisterAttached(
        "TrackSelection",
        typeof(bool),
        typeof(PageTransition),
        new PropertyMetadata(false, OnTrackSelectionChanged));

    /// <summary>
    /// 读取内容键.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <returns>当前内容键.</returns>
    public static object? GetTrigger(FrameworkElement element) => element.GetValue(TriggerProperty);

    /// <summary>
    /// 设置内容键.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <param name="value">内容键.</param>
    public static void SetTrigger(FrameworkElement element, object? value) => element.SetValue(TriggerProperty, value);

    /// <summary>
    /// 读取「显示时播放」开关.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <returns>是否在显示时播放.</returns>
    public static bool GetAnimateOnShow(FrameworkElement element) => (bool)element.GetValue(AnimateOnShowProperty);

    /// <summary>
    /// 设置「显示时播放」开关.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <param name="value">是否在显示时播放.</param>
    public static void SetAnimateOnShow(FrameworkElement element, bool value) => element.SetValue(AnimateOnShowProperty, value);

    /// <summary>
    /// 读取宿主场景.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <returns>切换场景.</returns>
    public static PageTransitionKind GetKind(FrameworkElement element)
        => (PageTransitionKind)element.GetValue(KindProperty);

    /// <summary>
    /// 设置宿主场景.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <param name="value">切换场景.</param>
    public static void SetKind(FrameworkElement element, PageTransitionKind value)
        => element.SetValue(KindProperty, value);

    /// <summary>
    /// 读取真正滑动的内容元素.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <returns>内容元素；未设置时为 <see langword="null"/>.</returns>
    public static FrameworkElement? GetContent(FrameworkElement element)
        => (FrameworkElement?)element.GetValue(ContentProperty);

    /// <summary>
    /// 设置真正滑动的内容元素.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <param name="value">内容元素.</param>
    public static void SetContent(FrameworkElement element, FrameworkElement? value)
        => element.SetValue(ContentProperty, value);

    /// <summary>
    /// 读取选择跟踪开关.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <returns>是否跟踪选择方向.</returns>
    public static bool GetTrackSelection(FrameworkElement element) => (bool)element.GetValue(TrackSelectionProperty);

    /// <summary>
    /// 设置选择跟踪开关.
    /// </summary>
    /// <param name="element">目标元素.</param>
    /// <param name="value">是否跟踪选择方向.</param>
    public static void SetTrackSelection(FrameworkElement element, bool value)
        => element.SetValue(TrackSelectionProperty, value);

    /// <summary>
    /// 立即在元素上播放一次入场过渡；重复调用会从起点重新播放.
    /// </summary>
    /// <param name="element">要移动的内容宿主.</param>
    /// <param name="kind">指定场景；为 <see langword="null"/> 时读取附加属性.</param>
    public static void Play(FrameworkElement element, PageTransitionKind? kind = null)
    {
        try
        {
            var attachedKind = kind ?? GetKind(element);
            var resolvedKind = attachedKind;
            var pending = PageTransitionSession.CurrentAt(DateTimeOffset.Now);
            if (resolvedKind == PageTransitionKind.Auto)
            {
                resolvedKind = pending?.Kind ?? PageTransitionKind.Tab;
            }

            var resolvedDirection = PageTransitionPolicy.GetDefaultDirection(resolvedKind);
            if (resolvedKind != PageTransitionKind.Page
                && pending is not null
                && PageTransitionPolicy.AcceptsPending(attachedKind, pending.Kind))
            {
                // 场景一致时沿用选择控件发布的方向；标签是左右、侧栏是上下.
                resolvedDirection = pending.Direction;
            }

            var animationsEnabled = AnimationsEnabled();
            var (x, y) = PageTransitionPolicy.GetOffset(resolvedKind, resolvedDirection);
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            ElementCompositionPreview.SetIsTranslationEnabled(element, true);
            visual.Clip ??= compositor.CreateInsetClip();

            // 参考动态页的分区切换：外框保持不动，只让新内容在固定的裁切范围内滑入.
            var translationTarget = resolvedKind == PageTransitionKind.Page
                ? element
                : (GetContent(element) ?? element);
            var targetVisual = ReferenceEquals(translationTarget, element)
                ? visual
                : ElementCompositionPreview.GetElementVisual(translationTarget);
            ElementCompositionPreview.SetIsTranslationEnabled(translationTarget, true);

            // 先停下上一段动画并立即落到本段动画的起点，再启动时间线。
            // 不能先把元素复位到终点再启动动画：那样已加载的内容会先以终态闪现一帧，
            // 看上去像被移走后再重新进入，只有首次（内容还没渲染）才显得自然.
            visual.StopAnimation("Opacity");
            visual.StopAnimation("Translation");
            if (!ReferenceEquals(targetVisual, visual))
            {
                targetVisual.StopAnimation("Translation");
                visual.Properties.InsertVector3("Translation", Vector3.Zero);
            }

            if (!animationsEnabled)
            {
                targetVisual.Properties.InsertVector3("Translation", Vector3.Zero);
                visual.Opacity = 1f;
                return;
            }

            targetVisual.Properties.InsertVector3("Translation", new Vector3(x, y, 0f));
            visual.Opacity = PageTransitionPolicy.GetStartOpacity(resolvedKind);

            var (easeX1, easeY1, easeX2, easeY2) = PageTransitionPolicy.GetEasing(resolvedKind);
            var easing = compositor.CreateCubicBezierEasingFunction(
                new Vector2(easeX1, easeY1),
                new Vector2(easeX2, easeY2));

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.Duration = PageTransitionPolicy.GetFadeDuration(resolvedKind);
            fade.InsertKeyFrame(0f, PageTransitionPolicy.GetStartOpacity(resolvedKind));
            fade.InsertKeyFrame(1f, 1f);
            visual.StartAnimation("Opacity", fade);

            var slide = compositor.CreateVector3KeyFrameAnimation();
            slide.Duration = PageTransitionPolicy.GetSlideDuration(resolvedKind);
            slide.InsertKeyFrame(0f, new Vector3(x, y, 0f));
            slide.InsertKeyFrame(1f, Vector3.Zero, easing);
            targetVisual.StartAnimation("Translation", slide);
        }
        catch (Exception ex)
        {
            // 动画只是增强：组合层不可用时直接显示最终状态，切换照常完成.
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static void OnTriggerChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var state = States.GetValue(element, static _ => new TransitionState());
        var previous = state.TriggerValue;
        var hasPrevious = state.HasTrigger;
        state.HasTrigger = true;
        state.TriggerValue = args.NewValue;

        if (!PageTransitionPolicy.ShouldAnimateTrigger(
                element.IsLoaded,
                AnimationsEnabled(),
                hasPrevious,
                previous,
                args.NewValue))
        {
            return;
        }

        Play(element);
    }

    private static void OnAnimateOnShowChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var state = States.GetValue(element, static _ => new TransitionState());
        var enabled = args.NewValue is true;
        if (!enabled)
        {
            if (state.VisibilityToken != 0)
            {
                element.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, state.VisibilityToken);
                state.VisibilityToken = 0;
            }

            return;
        }

        if (state.VisibilityToken != 0)
        {
            return;
        }

        state.VisibilityToken = element.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (target, _) =>
        {
            // 回调只在可见度变化时触发；从折叠变为可见且已加载才播放.
            if (target is not FrameworkElement host
                || host.Visibility != Visibility.Visible
                || !PageTransitionPolicy.ShouldAnimateShow(host.IsLoaded, AnimationsEnabled(), wasVisible: false))
            {
                return;
            }

            Play(host);
        });
    }

    private static void OnTrackSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var state = States.GetValue(element, static _ => new TransitionState());
        if (args.NewValue is not true)
        {
            DetachSelection(state);
            return;
        }

        if (state.SelectionTracker is not null)
        {
            return;
        }

        if (element is ItemsView itemsView)
        {
            state.SelectionTracker = itemsView;
            state.ItemsViewHandler = OnItemsViewSelectionChanged;
            itemsView.SelectionChanged += state.ItemsViewHandler;
            state.LastIndex = PageTransitionPolicy.IndexOf(itemsView.ItemsSource, itemsView.SelectedItem);
        }
        else if (element is SelectorBar selectorBar)
        {
            state.SelectionTracker = selectorBar;
            state.SelectorBarHandler = OnSelectorBarSelectionChanged;
            selectorBar.SelectionChanged += state.SelectorBarHandler;
            state.LastIndex = PageTransitionPolicy.IndexOf(selectorBar.Items, selectorBar.SelectedItem);
        }
    }

    private static void DetachSelection(TransitionState state)
    {
        if (state.SelectionTracker is ItemsView itemsView && state.ItemsViewHandler is not null)
        {
            itemsView.SelectionChanged -= state.ItemsViewHandler;
        }
        else if (state.SelectionTracker is SelectorBar selectorBar && state.SelectorBarHandler is not null)
        {
            selectorBar.SelectionChanged -= state.SelectorBarHandler;
        }

        state.SelectionTracker = null;
        state.ItemsViewHandler = null;
        state.SelectorBarHandler = null;
        state.LastIndex = -1;
    }

    private static void OnItemsViewSelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs _)
    {
        var state = States.GetValue(sender, static _ => new TransitionState());
        PublishSelection(state, PageTransitionPolicy.IndexOf(sender.ItemsSource, sender.SelectedItem));
    }

    private static void OnSelectorBarSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs _)
    {
        var state = States.GetValue(sender, static _ => new TransitionState());
        PublishSelection(state, PageTransitionPolicy.IndexOf(sender.Items, sender.SelectedItem));
    }

    private static void PublishSelection(TransitionState state, int index)
    {
        if (index < 0)
        {
            state.LastIndex = -1;
            return;
        }

        var previous = state.LastIndex;
        state.LastIndex = index;
        if (previous >= 0 && previous != index)
        {
            PageTransitionSession.Publish(
                PageTransitionKind.Sidebar,
                previous,
                index,
                DateTimeOffset.Now);
        }
    }

    private static bool AnimationsEnabled()
    {
        try
        {
            _uiSettings ??= new UISettings();
            return _uiSettings.AnimationsEnabled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return false;
        }
    }

    private sealed class TransitionState
    {
        public bool HasTrigger { get; set; }

        public object? TriggerValue { get; set; }

        public long VisibilityToken { get; set; }

        public FrameworkElement? SelectionTracker { get; set; }

        public TypedEventHandler<ItemsView, ItemsViewSelectionChangedEventArgs>? ItemsViewHandler { get; set; }

        public TypedEventHandler<SelectorBar, SelectorBarSelectionChangedEventArgs>? SelectorBarHandler { get; set; }

        public int LastIndex { get; set; } = -1;
    }
}
