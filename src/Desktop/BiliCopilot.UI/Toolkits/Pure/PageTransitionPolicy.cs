// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 内容切换时入场滑动的方向.
/// </summary>
public enum PageTransitionDirection
{
    /// <summary>
    /// 从右侧滑入.
    /// </summary>
    FromRight,

    /// <summary>
    /// 从左侧滑入.
    /// </summary>
    FromLeft,

    /// <summary>
    /// 从上方滑入.
    /// </summary>
    FromTop,

    /// <summary>
    /// 从下方滑入.
    /// </summary>
    FromBottom,
}

/// <summary>
/// 页面、标签与侧栏三类切换场景.
/// </summary>
public enum PageTransitionKind
{
    /// <summary>
    /// 跟随最近一次选择自动判断场景.
    /// </summary>
    Auto,

    /// <summary>
    /// 整页切换：经典上浮入场.
    /// </summary>
    Page,

    /// <summary>
    /// 标签切换：横向滑动，方向由标签顺序决定.
    /// </summary>
    Tab,

    /// <summary>
    /// 侧栏切换：纵向滑动，方向由列表顺序决定.
    /// </summary>
    Sidebar,
}

/// <summary>
/// 最近一次选择产生的过渡方向.
/// </summary>
/// <param name="Kind">产生方向的选择场景.</param>
/// <param name="Direction">入场方向.</param>
/// <param name="PublishedAt">发布方向的时间.</param>
public sealed record PageTransitionRequest(
    PageTransitionKind Kind,
    PageTransitionDirection Direction,
    DateTimeOffset PublishedAt);

/// <summary>
/// 三类切换过渡的纯决策与参数.
/// </summary>
/// <remarks>
/// 页面切换统一上浮；标签按标签栏顺序左右滑入；侧栏按列表顺序上下滑入。
/// 方向由选择控件发布，内容宿主只消费最近一次有效选择，避免各处各写一套判断.
/// </remarks>
public static class PageTransitionPolicy
{
    /// <summary>
    /// 选择方向的有效期；超过后内容变化不再沿用旧方向.
    /// </summary>
    public static TimeSpan RequestLifetime { get; } = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// 标签滑动距离.
    /// </summary>
    public const float TabOffset = 40f;

    /// <summary>
    /// 侧栏滑动距离.
    /// </summary>
    public const float SidebarOffset = 24f;

    /// <summary>
    /// 页面切换的上浮距离.
    /// </summary>
    public const float PageOffset = 24f;

    /// <summary>
    /// 判断绑定的内容键变化时是否值得播放过渡.
    /// </summary>
    /// <param name="isLoaded">宿主是否已经加载（首次数据绑定不播放）.</param>
    /// <param name="animationsEnabled">系统动画是否开启.</param>
    /// <param name="hasPreviousValue">此前是否已经收到过键值.</param>
    /// <param name="previousValue">此前的键值.</param>
    /// <param name="currentValue">新的键值.</param>
    /// <returns>可以播放时为 <see langword="true"/>.</returns>
    public static bool ShouldAnimateTrigger(
        bool isLoaded,
        bool animationsEnabled,
        bool hasPreviousValue,
        object? previousValue,
        object? currentValue)
        => isLoaded
            && animationsEnabled
            && hasPreviousValue
            && currentValue is not null
            && !ReferenceEquals(previousValue, currentValue);

    /// <summary>
    /// 判断元素由隐藏变为显示时是否值得播放过渡.
    /// </summary>
    /// <param name="isLoaded">元素是否已经加载.</param>
    /// <param name="animationsEnabled">系统动画是否开启.</param>
    /// <param name="wasVisible">变化前是否可见.</param>
    /// <returns>可以播放时为 <see langword="true"/>.</returns>
    public static bool ShouldAnimateShow(bool isLoaded, bool animationsEnabled, bool wasVisible)
        => isLoaded && animationsEnabled && !wasVisible;

    /// <summary>
    /// 按列表中的先后顺序取本次切换的入场方向.
    /// </summary>
    /// <param name="kind">切换场景.</param>
    /// <param name="previousIndex">上一项的序号.</param>
    /// <param name="currentIndex">新选中项的序号.</param>
    /// <returns>入场方向；序号无效或没有变化时按默认方向.</returns>
    public static PageTransitionDirection GetStepDirection(
        PageTransitionKind kind,
        int previousIndex,
        int currentIndex)
    {
        if (previousIndex < 0 || currentIndex < 0 || previousIndex == currentIndex)
        {
            return GetDefaultDirection(kind);
        }

        return kind switch
        {
            PageTransitionKind.Tab => currentIndex > previousIndex
                ? PageTransitionDirection.FromRight
                : PageTransitionDirection.FromLeft,

            // 侧栏是左侧列表，选中项往下移动时内容从上往下落，选中项往上时内容从下往上升，
            // 与手指/选择移动的方向一致.
            PageTransitionKind.Sidebar => currentIndex > previousIndex
                ? PageTransitionDirection.FromTop
                : PageTransitionDirection.FromBottom,
            _ => PageTransitionDirection.FromBottom,
        };
    }

    /// <summary>
    /// 取场景的默认入场方向.
    /// </summary>
    /// <param name="kind">切换场景.</param>
    /// <returns>默认方向.</returns>
    public static PageTransitionDirection GetDefaultDirection(PageTransitionKind kind)
        => kind switch
        {
            PageTransitionKind.Page => PageTransitionDirection.FromBottom,
            PageTransitionKind.Sidebar => PageTransitionDirection.FromBottom,
            _ => PageTransitionDirection.FromRight,
        };

    /// <summary>
    /// 判断宿主是否接受某个选择场景发布的方向.
    /// </summary>
    /// <param name="attachedKind">宿主声明的场景.</param>
    /// <param name="pendingKind">最近一次选择所属的场景.</param>
    /// <returns>声明为自动或场景一致时为 <see langword="true"/>.</returns>
    public static bool AcceptsPending(PageTransitionKind attachedKind, PageTransitionKind pendingKind)
        => attachedKind == PageTransitionKind.Auto || attachedKind == pendingKind;

    /// <summary>
    /// 取入场时的起始透明度.
    /// </summary>
    /// <param name="kind">切换场景.</param>
    /// <returns>起始透明度.</returns>
    public static float GetStartOpacity(PageTransitionKind kind)
        => kind switch
        {
            PageTransitionKind.Page => 0f,
            PageTransitionKind.Sidebar => 0.7f,
            _ => 0.65f,
        };

    /// <summary>
    /// 取淡入时长.
    /// </summary>
    /// <param name="kind">切换场景.</param>
    /// <returns>淡入时长.</returns>
    public static TimeSpan GetFadeDuration(PageTransitionKind kind)
        => kind == PageTransitionKind.Page
            ? TimeSpan.FromMilliseconds(250)
            : TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 取滑动时长.
    /// </summary>
    /// <param name="kind">切换场景.</param>
    /// <returns>滑动时长.</returns>
    public static TimeSpan GetSlideDuration(PageTransitionKind kind)
        => kind == PageTransitionKind.Page
            ? TimeSpan.FromMilliseconds(300)
            : TimeSpan.FromMilliseconds(280);

    /// <summary>
    /// 取滑动缓动控制点.
    /// </summary>
    /// <param name="kind">切换场景.</param>
    /// <returns>cubic-bezier 的 x1/y1/x2/y2.</returns>
    public static (float X1, float Y1, float X2, float Y2) GetEasing(PageTransitionKind kind)
        => kind switch
        {
            // 页面用标准减速曲线；标签与侧栏沿用动态页的强减速曲线，保证滑动手感一致.
            PageTransitionKind.Page => (0.1f, 0.9f, 0.2f, 1f),
            _ => (0.2f, 0f, 0f, 1f),
        };

    /// <summary>
    /// 按场景与方向取起始偏移；终点都是原点.
    /// </summary>
    /// <param name="kind">切换场景.</param>
    /// <param name="direction">入场方向.</param>
    /// <returns>起始 X/Y 偏移.</returns>
    public static (float X, float Y) GetOffset(PageTransitionKind kind, PageTransitionDirection direction)
        => kind switch
        {
            PageTransitionKind.Page => (0f, PageOffset),
            PageTransitionKind.Sidebar => direction == PageTransitionDirection.FromTop
                ? (0f, -SidebarOffset)
                : (0f, SidebarOffset),
            _ => direction == PageTransitionDirection.FromLeft
                ? (-TabOffset, 0f)
                : (TabOffset, 0f),
        };

    /// <summary>
    /// 在列表里查找元素序号；引用优先，其次按相等比较.
    /// </summary>
    /// <param name="itemsSource">列表数据源.</param>
    /// <param name="item">要查找的元素.</param>
    /// <returns>序号；找不到时为 -1.</returns>
    public static int IndexOf(object? itemsSource, object? item)
    {
        if (itemsSource is null || item is null || itemsSource is not System.Collections.IEnumerable enumerable)
        {
            return -1;
        }

        var index = 0;
        foreach (var candidate in enumerable)
        {
            if (ReferenceEquals(candidate, item) || Equals(candidate, item))
            {
                return index;
            }

            index++;
        }

        return -1;
    }
}

/// <summary>
/// 最近一次选择发布的过渡方向.
/// </summary>
/// <remarks>
/// 选择控件在选中项变化时发布方向，内容宿主的触发器随后消费它；
/// 同一事件可能触及多个内容宿主，因此读取不清理，靠有效期与场景匹配避免串用.
/// </remarks>
public static class PageTransitionSession
{
    private static PageTransitionRequest? _current;

    /// <summary>
    /// 按指定时刻读取最近一次方向，过期时同时释放.
    /// </summary>
    /// <param name="now">当前时间.</param>
    /// <returns>仍有效的方向.</returns>
    public static PageTransitionRequest? CurrentAt(DateTimeOffset now)
    {
        var request = _current;
        if (request is null)
        {
            return null;
        }

        if (now < request.PublishedAt || now - request.PublishedAt > PageTransitionPolicy.RequestLifetime)
        {
            _current = null;
            return null;
        }

        return request;
    }

    /// <summary>
    /// 按上一项与新选中项的序号发布方向.
    /// </summary>
    /// <param name="kind">选择场景.</param>
    /// <param name="previousIndex">上一项的序号.</param>
    /// <param name="currentIndex">新选中项的序号.</param>
    /// <param name="now">发布时间.</param>
    public static void Publish(
        PageTransitionKind kind,
        int previousIndex,
        int currentIndex,
        DateTimeOffset now)
    {
        if (previousIndex < 0 || currentIndex < 0 || previousIndex == currentIndex)
        {
            return;
        }

        _current = new PageTransitionRequest(
            kind,
            PageTransitionPolicy.GetStepDirection(kind, previousIndex, currentIndex),
            now);
    }

    /// <summary>
    /// 释放待消费方向.
    /// </summary>
    public static void Clear() => _current = null;
}
