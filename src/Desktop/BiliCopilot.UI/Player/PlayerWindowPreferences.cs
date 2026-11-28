// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.Player;

/// <summary>
/// 播放窗口初始状态的纯逻辑解析：布局偏好与播放模式偏好到实际取值的映射.
/// </summary>
/// <remarks>
/// 播放模式偏好使用独立的 <see cref="PlayerPresenterModePreference"/> 枚举，
/// 与表示窗口实际形态的 <see cref="PlayerPresenterMode"/> 区分：后者只是普通/全屏/小窗三态。
/// 该类型不依赖 WinUI / 视图模型，因此可以直接被无窗口环境的单元测试引用。
/// </remarks>
public static class PlayerWindowPreferenceResolver
{
    /// <summary>
    /// 根据播放模式偏好与上次记忆的窗口模式，解析打开播放窗口时应进入的模式.
    /// </summary>
    /// <param name="preference">播放模式偏好.</param>
    /// <param name="lastRemembered">上次记忆的窗口模式；仅在偏好为记忆上次时使用.</param>
    /// <returns>启动时应使用的窗口模式.</returns>
    public static PlayerPresenterMode ResolveStartupPresenter(PlayerPresenterModePreference preference, PlayerPresenterMode lastRemembered)
        => preference switch
        {
            PlayerPresenterModePreference.Normal => PlayerPresenterMode.Default,
            PlayerPresenterModePreference.FullScreen => PlayerPresenterMode.FullScreen,
            PlayerPresenterModePreference.CompactOverlay => PlayerPresenterMode.CompactOverlay,
            // 记忆上次；记忆值中的其他取值（或异常值）一律回落到普通窗口.
            _ => lastRemembered is PlayerPresenterMode.FullScreen or PlayerPresenterMode.CompactOverlay
                ? lastRemembered
                : PlayerPresenterMode.Default,
        };

    /// <summary>
    /// 根据布局偏好与上次记忆的布局，解析打开播放窗口时是否使用默认布局（信息区+侧栏）.
    /// </summary>
    /// <param name="preference">布局偏好.</param>
    /// <param name="lastUsedDefaultLayout">上次记忆是否为默认布局；仅在偏好为记忆上次时使用.</param>
    /// <returns><see langword="true"/> 表示使用默认布局，<see langword="false"/> 表示铺满布局.</returns>
    public static bool ResolveInitialDefaultLayout(PlayerLayoutPreference preference, bool lastUsedDefaultLayout)
        => preference switch
        {
            PlayerLayoutPreference.Fill => false,
            PlayerLayoutPreference.Default => true,
            _ => lastUsedDefaultLayout,
        };
}
