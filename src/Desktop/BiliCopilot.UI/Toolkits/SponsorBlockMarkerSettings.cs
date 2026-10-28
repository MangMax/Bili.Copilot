// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 空降助手进度条标记的显示开关.
/// </summary>
/// <remarks>
/// 标记只是展示，关闭它不影响跳过 / 静音 / 跳过按钮等行为；
/// 提供这个开关是为了让不想要色块的用户不必靠把颜色调成全透明来“藏”起来.
/// </remarks>
internal static class SponsorBlockMarkerSettings
{
    /// <summary>
    /// 开关变化时触发，供进度条重绘.
    /// </summary>
    public static event EventHandler? Changed;

    /// <summary>
    /// 是否在进度条上绘制片段色块.
    /// </summary>
    public static bool IsVisible
    {
        get
        {
            try
            {
                return SettingsToolkit.ReadLocalSetting(SettingNames.SponsorBlockMarkerVisible, true);
            }
            catch (Exception)
            {
                // 本地设置不可用时按显示处理，保持与默认值一致.
                return true;
            }
        }

        set
        {
            if (IsVisible == value)
            {
                return;
            }

            SettingsToolkit.WriteLocalSetting(SettingNames.SponsorBlockMarkerVisible, value);
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
