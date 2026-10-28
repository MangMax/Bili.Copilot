// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.SponsorBlock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 空降助手单个类别的行为与标记颜色设置项.
/// </summary>
public sealed class SponsorBlockCategorySettingViewModel : ObservableObject
{
    private readonly Action<SponsorBlockCategory, SponsorBlockBehavior> _onBehaviorChanged;
    private readonly Action<SponsorBlockCategory, uint>? _onCustomColorChanged;
    private readonly Action<SponsorBlockCategory>? _onCustomColorReset;
    private uint _defaultColor;
    private SponsorBlockBehavior _behavior;
    private SponsorBlockBehavior _persistedBehavior;
    private uint? _customColor;
    private int _bindingRefreshDepth;

    /// <summary>
    /// Initializes a new instance of the <see cref="SponsorBlockCategorySettingViewModel"/> class.
    /// </summary>
    /// <param name="category">片段类别.</param>
    /// <param name="behavior">当前行为配置.</param>
    /// <param name="displayName">类别的本地化名称.</param>
    /// <param name="onBehaviorChanged">行为变化时的回调.</param>
    /// <param name="customColor">当前自定义颜色；<c>null</c> 表示使用主题色.</param>
    /// <param name="onCustomColorChanged">设置自定义颜色时的回调.</param>
    /// <param name="onCustomColorReset">恢复主题色时的回调.</param>
    /// <param name="themeColorProvider">
    /// 解析类别主题色板颜色的回调，仅用于给出初始值；
    /// 真正的主题相关取值由视图层在加载和主题变化时通过 <see cref="UpdateDefaultColor"/> 推入，
    /// 因为主题是元素级的，纯逻辑层无法判断. 不传时退化为
    /// <see cref="SponsorBlockCategoryPalette.GetFallbackColor"/>，便于纯逻辑测试.
    /// </param>
    /// <param name="defaultColorText">「未自定义」时右侧显示的文案.</param>
    public SponsorBlockCategorySettingViewModel(
        SponsorBlockCategory category,
        SponsorBlockBehavior behavior,
        string displayName,
        Action<SponsorBlockCategory, SponsorBlockBehavior> onBehaviorChanged,
        uint? customColor = null,
        Action<SponsorBlockCategory, uint>? onCustomColorChanged = null,
        Action<SponsorBlockCategory>? onCustomColorReset = null,
        Func<SponsorBlockCategory, uint>? themeColorProvider = null,
        string defaultColorText = "")
    {
        Category = category;
        _behavior = behavior;
        _persistedBehavior = behavior;
        DisplayName = displayName;
        _onBehaviorChanged = onBehaviorChanged;
        _customColor = customColor;
        _onCustomColorChanged = onCustomColorChanged;
        _onCustomColorReset = onCustomColorReset;
        _defaultColor = (themeColorProvider ?? SponsorBlockCategoryPalette.GetFallbackColor)(category);
        DefaultColorText = defaultColorText;
    }

    /// <summary>
    /// 片段类别.
    /// </summary>
    public SponsorBlockCategory Category { get; }

    /// <summary>
    /// 类别的本地化名称.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 「未自定义」时右侧显示的文案（例如「默认」）.
    /// </summary>
    public string DefaultColorText { get; }

    /// <summary>
    /// 可选的行为列表.
    /// </summary>
    public IReadOnlyList<SponsorBlockBehavior> AvailableBehaviors { get; }
        = Enum.GetValues<SponsorBlockBehavior>();

    /// <summary>
    /// 当前行为配置.
    /// <para>
    /// 只有值真正变化、且与该类别已保存的值不同时才写入本地设置，
    /// 这样下拉框在绑定过程中的重复赋值不会把不该保存的值写进设置.
    /// </para>
    /// </summary>
    public SponsorBlockBehavior Behavior
    {
        get => _behavior;
        set
        {
            if (IsBindingRefreshInProgress || !SetProperty(ref _behavior, value) || value == _persistedBehavior)
            {
                return;
            }

            _persistedBehavior = value;
            _onBehaviorChanged(Category, value);
        }
    }

    /// <summary>
    /// 当前自定义颜色（0xAARRGGBB）；<c>null</c> 表示使用主题色.
    /// </summary>
    public uint? CustomColor => _customColor;

    /// <summary>
    /// 是否设置了自定义颜色.
    /// </summary>
    public bool HasCustomColor => _customColor.HasValue;

    /// <summary>
    /// 是否仍在使用主题色，用于设置页显示「默认」标记.
    /// </summary>
    public bool IsUsingDefaultColor => !_customColor.HasValue;

    /// <summary>
    /// 该类别当前生效的颜色（0xAARRGGBB）：自定义颜色优先，否则为主题色板颜色.
    /// <para>
    /// 色块始终按这里的取值绘制，因此未自定义时也能看到实际会用的默认色，
    /// 而不是一个空白占位.
    /// </para>
    /// </summary>
    public uint EffectiveColor => _customColor ?? _defaultColor;

    /// <summary>
    /// 色块右侧始终显示的文本：自定义时是颜色值，未自定义时是「默认」.
    /// </summary>
    public string ColorDisplayText
        => _customColor is uint argb ? SponsorBlockColorParser.ToDisplayString(argb) : DefaultColorText;

    /// <summary>
    /// 颜色按钮的无障碍自动化 ID.
    /// </summary>
    public string ColorAutomationId => $"SponsorBlockColor_{Category}";

    /// <summary>
    /// 「恢复默认颜色」按钮的无障碍自动化 ID.
    /// </summary>
    public string ColorResetAutomationId => $"SponsorBlockColorReset_{Category}";

    private bool IsBindingRefreshInProgress => _bindingRefreshDepth > 0;

    /// <summary>
    /// 接收下拉框的选中项变化，只在选中项是合法行为时生效.
    /// </summary>
    /// <param name="selectedItem">下拉框当前的选中项.</param>
    public void ApplySelection(object? selectedItem)
    {
        if (selectedItem is SponsorBlockBehavior behavior && AvailableBehaviors.Contains(behavior))
        {
            Behavior = behavior;
        }
    }

    /// <summary>
    /// 设置自定义颜色，值未变化时不重复保存.
    /// </summary>
    /// <param name="argb">0xAARRGGBB 格式的颜色值.</param>
    public void SetCustomColor(uint argb)
    {
        if (_customColor == argb)
        {
            return;
        }

        _customColor = argb;
        RaiseColorChanged();
        _onCustomColorChanged?.Invoke(Category, argb);
    }

    /// <summary>
    /// 恢复主题色，未设置自定义颜色时不重复保存.
    /// </summary>
    public void ResetColor()
    {
        if (!_customColor.HasValue)
        {
            return;
        }

        _customColor = null;
        RaiseColorChanged();
        _onCustomColorReset?.Invoke(Category);
    }

    /// <summary>
    /// 开始一次绑定刷新。
    /// <para>
    /// 刷新期间控件会把数据项换绑到下拉框上，随之而来的值变化不代表用户操作，
    /// 这段时间内对 <see cref="Behavior"/> 的写入会被忽略.
    /// </para>
    /// </summary>
    public void BeginBindingRefresh() => _bindingRefreshDepth++;

    /// <summary>
    /// 结束一次由 <see cref="BeginBindingRefresh"/> 开启的绑定刷新.
    /// </summary>
    public void EndBindingRefresh()
    {
        if (_bindingRefreshDepth > 0)
        {
            _bindingRefreshDepth--;
        }
    }

    /// <summary>
    /// 更新「未自定义」时使用的默认色.
    /// <para>
    /// 默认色取自主题色板，而主题是元素级的，只能由视图层解析后推入；
    /// 自定义颜色是字面值，不受主题影响，因此有自定义色时直接忽略.
    /// </para>
    /// </summary>
    /// <param name="argb">该类别在当前主题下的色板颜色.</param>
    public void UpdateDefaultColor(uint argb)
    {
        if (HasCustomColor || _defaultColor == argb)
        {
            return;
        }

        _defaultColor = argb;
        OnPropertyChanged(nameof(EffectiveColor));
    }

    private void RaiseColorChanged()
    {
        OnPropertyChanged(nameof(CustomColor));
        OnPropertyChanged(nameof(HasCustomColor));
        OnPropertyChanged(nameof(IsUsingDefaultColor));
        OnPropertyChanged(nameof(EffectiveColor));
        OnPropertyChanged(nameof(ColorDisplayText));
    }
}
