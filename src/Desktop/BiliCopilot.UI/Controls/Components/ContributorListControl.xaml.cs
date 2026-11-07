// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 头像名称列表：每个条目显示圆形头像与用户名，点击进入用户空间.
/// </summary>
/// <remarks>
/// 联合投稿场景下替代原来拼接成一段文字的投稿人姓名列表；
/// 条目类型固定为 <see cref="VideoContributorViewModel"/>.
/// </remarks>
public sealed partial class ContributorListControl : UserControl
{
    /// <summary>
    /// <see cref="ItemsSource"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(ContributorListControl), new PropertyMetadata(default));

    /// <summary>
    /// <see cref="Header"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(ContributorListControl), new PropertyMetadata(default));

    /// <summary>
    /// Initializes a new instance of the <see cref="ContributorListControl"/> class.
    /// </summary>
    public ContributorListControl() => InitializeComponent();

    /// <summary>
    /// 条目集合，元素类型为 <see cref="VideoContributorViewModel"/>.
    /// </summary>
    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// 列表标题；为空时不显示标题.
    /// </summary>
    public string? Header
    {
        get => (string?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }
}