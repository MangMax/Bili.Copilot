// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.SponsorBlock;

namespace BiliCopilot.UI.Toolkits;

/// <summary>
/// 空降助手按类别配置的自定义标记颜色.
/// </summary>
/// <remarks>
/// 存储实现交给 <see cref="SponsorBlockColorStore"/>，这里只把本地设置容器接上去，
/// 并对外暴露静态的 <see cref="Changed"/> 事件，让进度条等控件无需依赖设置页即可获知颜色变化.
/// </remarks>
internal static class SponsorBlockColorSettings
{
    private static readonly SponsorBlockColorStore _store = new(new SettingsStorage());

    /// <summary>
    /// 任一类别颜色变化时触发（写入或恢复默认）.
    /// </summary>
    public static event EventHandler? Changed
    {
        add => _store.Changed += value;
        remove => _store.Changed -= value;
    }

    /// <summary>
    /// 读取指定类别的自定义颜色.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>自定义颜色的 0xAARRGGBB 值；未配置时返回 <c>null</c>.</returns>
    public static uint? Read(SponsorBlockCategory category) => _store.Read(category);

    /// <summary>
    /// 写入指定类别的自定义颜色.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <param name="argb">0xAARRGGBB 格式的颜色值.</param>
    public static void Write(SponsorBlockCategory category, uint argb) => _store.Write(category, argb);

    /// <summary>
    /// 恢复指定类别的主题色.
    /// </summary>
    /// <param name="category">片段类别.</param>
    public static void Reset(SponsorBlockCategory category) => _store.Reset(category);

    /// <summary>
    /// 用本地设置容器实现的存储后端.
    /// </summary>
    private sealed class SettingsStorage : ISponsorBlockColorStorage
    {
        /// <inheritdoc/>
        public string? Read(string key)
        {
            if (ToSettingName(key) is not SettingNames name)
            {
                return null;
            }

            try
            {
                // 键不存在时读取会得到空串，与“未配置”等价，统一交给解析器判定.
                return SettingsToolkit.IsSettingKeyExist(name)
                    ? SettingsToolkit.ReadLocalSetting(name, string.Empty)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public void Write(string key, string value)
        {
            if (ToSettingName(key) is SettingNames name)
            {
                SettingsToolkit.WriteLocalSetting(name, value);
            }
        }

        /// <inheritdoc/>
        public void Delete(string key)
        {
            if (ToSettingName(key) is SettingNames name)
            {
                SettingsToolkit.DeleteLocalSetting(name);
            }
        }

        private static SettingNames? ToSettingName(string key)
            => Enum.TryParse<SettingNames>(key, out var name) ? name : null;
    }
}
