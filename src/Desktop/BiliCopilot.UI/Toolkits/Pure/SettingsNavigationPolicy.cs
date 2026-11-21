// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 设置页面的导航分区.
/// </summary>
public enum SettingsSection
{
    /// <summary>
    /// 设置首页（分类入口、轻量推荐项与关于）.
    /// </summary>
    Root,

    /// <summary>
    /// 播放设置（播放控制、播放行为、MPV 附加设置与空降助手）.
    /// </summary>
    Playback,

    /// <summary>
    /// 外观与导航（主题、顶部/侧边导航栏与滚动加速）.
    /// </summary>
    AppearanceAndNavigation,

    /// <summary>
    /// 下载与存储（下载、截图与图片缓存）.
    /// </summary>
    StorageAndDownload,

    /// <summary>
    /// 通知与系统（通知、托盘驻留与日志）.
    /// </summary>
    SystemAndNotification,

    /// <summary>
    /// 应用更新（检查更新、下载安装、更新说明与更新偏好）.
    /// </summary>
    Update,

    /// <summary>
    /// AI 服务（流式响应、提示词模板与模型服务配置）.
    /// </summary>
    AIService,
}

/// <summary>
/// 设置页面二级导航与深链接解析的纯决策逻辑.
/// </summary>
public static class SettingsNavigationPolicy
{
    /// <summary>
    /// 应用更新通知使用的深链接页面标识.
    /// </summary>
    public const string UpdateNotificationRoute = "settings-update";

    /// <summary>
    /// 将导航参数解析为目标设置分区；未识别时回到设置首页.
    /// </summary>
    /// <param name="parameter">页面导航参数或路由名称.</param>
    /// <returns>目标设置分区.</returns>
    public static SettingsSection ResolveSection(object? parameter)
    {
        if (parameter is SettingsSection section)
        {
            return Enum.IsDefined(section) ? section : SettingsSection.Root;
        }

        if (parameter is not string route)
        {
            return SettingsSection.Root;
        }

        if (route == UpdateNotificationRoute)
        {
            return SettingsSection.Update;
        }

        if (Enum.TryParse<SettingsSection>(route, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        return SettingsSection.Root;
    }

    /// <summary>
    /// 判断指定分区是否为二级页面.
    /// </summary>
    /// <param name="section">设置分区.</param>
    /// <returns>为二级页面时返回 <see langword="true"/>.</returns>
    public static bool IsSubpage(SettingsSection section) => section != SettingsSection.Root;

    /// <summary>
    /// 尝试从当前分区返回上一级（设置首页）.
    /// </summary>
    /// <param name="current">当前分区.</param>
    /// <param name="target">目标分区.</param>
    /// <returns>实际发生了返回时为 <see langword="true"/>.</returns>
    public static bool TryGoBack(SettingsSection current, out SettingsSection target)
    {
        target = SettingsSection.Root;
        return current != SettingsSection.Root;
    }

    /// <summary>
    /// 计算两个分区间横向切换过渡的逻辑序号对，供 <see cref="PageTransitionSession"/> 发布方向.
    /// </summary>
    /// <param name="from">来源分区.</param>
    /// <param name="to">目标分区.</param>
    /// <returns>来源与目标逻辑序号；同一分区时返回 (-1, -1).</returns>
    public static (int PreviousIndex, int CurrentIndex) GetTransitionIndices(SettingsSection from, SettingsSection to)
    {
        if (from == to)
        {
            return (-1, -1);
        }

        return to == SettingsSection.Root ? (1, 0) : (0, 1);
    }

    /// <summary>
    /// 判断离开当前分区前往目标分区时是否需要保存 AI 服务配置.
    /// </summary>
    /// <param name="from">来源分区.</param>
    /// <param name="to">目标分区.</param>
    /// <returns>离开 AI 服务二级页时为 <see langword="true"/>.</returns>
    public static bool ShouldSaveAIServicesOnLeave(SettingsSection from, SettingsSection to)
        => from == SettingsSection.AIService && to != SettingsSection.AIService;

    /// <summary>
    /// 判断进入目标分区时是否需要初始化 AI 服务配置.
    /// </summary>
    /// <param name="target">目标分区.</param>
    /// <returns>进入 AI 服务二级页时为 <see langword="true"/>.</returns>
    public static bool ShouldInitializeAIServicesOnEnter(SettingsSection target)
        => target == SettingsSection.AIService;

    /// <summary>
    /// 判断进入目标分区时是否需要刷新图片缓存大小.
    /// </summary>
    /// <param name="target">目标分区.</param>
    /// <returns>进入下载与存储二级页时为 <see langword="true"/>.</returns>
    public static bool ShouldRefreshCacheSizeOnEnter(SettingsSection target)
        => target == SettingsSection.StorageAndDownload;
}
