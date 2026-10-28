// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.SponsorBlock;

/// <summary>
/// 自定义标记颜色的存储后端，由界面层用本地设置实现.
/// </summary>
/// <remarks>
/// 这里只操作字符串键值，使核心逻辑与 WinUI / 本地设置容器解耦，
/// 可以直接在单元测试里替换为内存实现.
/// </remarks>
public interface ISponsorBlockColorStorage
{
    /// <summary>
    /// 读取指定键的原始字符串.
    /// </summary>
    /// <param name="key">设置项名称.</param>
    /// <returns>保存的字符串；不存在时返回 <c>null</c>.</returns>
    string? Read(string key);

    /// <summary>
    /// 写入指定键的字符串.
    /// </summary>
    /// <param name="key">设置项名称.</param>
    /// <param name="value">要保存的字符串.</param>
    void Write(string key, string value);

    /// <summary>
    /// 删除指定键，使该类别回到主题色.
    /// </summary>
    /// <param name="key">设置项名称.</param>
    void Delete(string key);
}

/// <summary>
/// 按类别读写自定义标记颜色.
/// </summary>
/// <remarks>
/// 保存的是 <c>#AARRGGBB</c> 字符串；未配置、空值或格式损坏一律视为“使用主题色”，
/// 因此旧版本残留或手工改坏的设置不会让进度条标记变成透明.
/// </remarks>
public sealed class SponsorBlockColorStore
{
    private readonly ISponsorBlockColorStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockColorStore"/> class.
    /// </summary>
    /// <param name="storage">实际存储后端.</param>
    public SponsorBlockColorStore(ISponsorBlockColorStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

    /// <summary>
    /// 任一类别颜色变化时触发，用于让正在播放的进度条刷新标记.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 读取指定类别的自定义颜色.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <returns>自定义颜色的 0xAARRGGBB 值；未配置或无效时返回 <c>null</c>.</returns>
    public uint? Read(SponsorBlockCategory category)
    {
        try
        {
            return SponsorBlockColorParser.TryParse(_storage.Read(GetKey(category)), out var argb)
                ? argb
                : null;
        }
        catch (Exception)
        {
            // 本地设置损坏或存储不可用时回退到主题色，而不是让设置页崩溃.
            return null;
        }
    }

    /// <summary>
    /// 写入指定类别的自定义颜色.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <param name="argb">0xAARRGGBB 格式的颜色值.</param>
    public void Write(SponsorBlockCategory category, uint argb)
    {
        _storage.Write(GetKey(category), SponsorBlockColorParser.Format(argb));
        OnChanged();
    }

    /// <summary>
    /// 删除指定类别的自定义颜色，使其回到主题色.
    /// </summary>
    /// <param name="category">片段类别.</param>
    public void Reset(SponsorBlockCategory category)
    {
        _storage.Delete(GetKey(category));
        OnChanged();
    }

    private static string GetKey(SponsorBlockCategory category)
        => SponsorBlockCategoryCatalog.Get(category).ColorSettingName;

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
