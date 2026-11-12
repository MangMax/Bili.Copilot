// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Logging;

/// <summary>
/// 日志级别设置的存取接口.
/// </summary>
public interface ILogLevelSettingsStore
{
    /// <summary>
    /// 判断设置项是否存在.
    /// </summary>
    /// <param name="key">设置项名称.</param>
    /// <returns>存在时返回 <c>true</c>.</returns>
    bool IsSettingKeyExist(string key);

    /// <summary>
    /// 写入设置项.
    /// </summary>
    /// <typeparam name="T">设置项类型.</typeparam>
    /// <param name="key">设置项名称.</param>
    /// <param name="value">设置项的值.</param>
    void Write<T>(string key, T value);
}
