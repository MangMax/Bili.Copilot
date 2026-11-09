// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// 单条聊天消息控件：直接以纯逻辑条目 <see cref="LiveChatMessageItem"/> 为视图模型。
/// </summary>
/// <remarks>
/// 版式与点播评论条目一致；正文复用 <c>EmoteTextBlock</c>，因此 [表情] 与评论走同一套渲染。
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
