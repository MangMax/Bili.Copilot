// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// 单条聊天消息控件：直接以纯逻辑条目 <see cref="LiveChatMessageItem"/> 为视图模型。
/// </summary>
/// <remarks>
/// 条目本身不依赖 WinUI（它要被单测直接构造），因此这里不用 <c>LayoutUserControlBase</c> 的
/// 视图模型约束，而是用普通依赖属性承载。
/// </remarks>
public sealed partial class LiveChatMessageControl : UserControl
{
    /// <summary>
    /// <see cref="ViewModel"/> 依赖属性.
    /// </summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(LiveChatMessageItem), typeof(LiveChatMessageControl), new PropertyMetadata(default));

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatMessageControl"/> class.
    /// </summary>
    public LiveChatMessageControl() => InitializeComponent();

    /// <summary>
    /// Gets or sets 聊天条目.
    /// </summary>
    public LiveChatMessageItem ViewModel
    {
        get => (LiveChatMessageItem)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
