// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// 单条聊天消息控件：以展示条目 <see cref="LiveChatMessageItem"/> 为视图模型。
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
        DependencyProperty.Register(nameof(ViewModel), typeof(LiveChatMessageItem), typeof(LiveChatMessageControl), new PropertyMetadata(default, OnViewModelChanged));

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatMessageControl"/> class.
    /// </summary>
    public LiveChatMessageControl()
    {
        InitializeComponent();
        var contextMenu = CreateContextMenu();
        ContextFlyout = contextMenu;
        MessageBody.SetTextContextFlyout(contextMenu);
        SuperChatBody.ContextFlyout = contextMenu;
        SuperChatBody.SelectionFlyout = null;
        ActualThemeChanged += (_, _) => ViewModel?.UpdateTheme(ActualTheme);
    }

    private MenuFlyout CreateContextMenu()
    {
        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = ResourceToolkit.GetLocalizedString(StringNames.EnterUserSpace) };
        var blockUser = new MenuFlyoutItem { Text = ResourceToolkit.GetLocalizedString(StringNames.LiveChatBlockUserAction) };
        var blockText = new MenuFlyoutItem { Text = ResourceToolkit.GetLocalizedString(StringNames.LiveChatBlockSelectionAction) };
        var copy = new MenuFlyoutItem { Text = ResourceToolkit.GetLocalizedString(StringNames.Copy) };
        LiveChatMessageItem? message = null;
        var selected = string.Empty;
        menu.Opening += (_, _) =>
        {
            message = ViewModel;
            selected = menu.Target switch
            {
                RichTextBlock rich => rich.SelectedText,
                TextBlock text => text.SelectedText,
                _ => string.Empty,
            };
            open.IsEnabled = blockUser.IsEnabled = message?.HasUser == true;
            blockText.Visibility = string.IsNullOrWhiteSpace(selected) ? Visibility.Collapsed : Visibility.Visible;
            copy.IsEnabled = message is not null;
        };
        open.Click += (_, _) => message?.OpenUserCommand.Execute(null);
        blockUser.Click += (_, _) => message?.BlockUserCommand.Execute(null);
        blockText.Click += (_, _) => message?.BlockText(selected);
        copy.Click += (_, _) => message?.CopyText(string.IsNullOrEmpty(selected) ? message.Text : selected);
        menu.Items.Add(open);
        menu.Items.Add(blockUser);
        menu.Items.Add(blockText);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(copy);
        return menu;
    }

    private static void OnViewModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (LiveChatMessageControl)sender;
        control.ViewModel?.UpdateTheme(control.ActualTheme);
    }

    /// <summary>
    /// Gets or sets 聊天条目.
    /// </summary>
    public LiveChatMessageItem ViewModel
    {
        get => (LiveChatMessageItem)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
