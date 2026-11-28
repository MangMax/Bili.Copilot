// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;

namespace BiliCopilot.UI.Controls.Danmaku;

public sealed partial class DanmakuSendDialog : ContentDialog
{
    public DanmakuSendViewModel ViewModel
    {
        get { return (DanmakuSendViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(DanmakuSendViewModel), typeof(DanmakuSendDialog), new PropertyMetadata(default));

    public DanmakuSendDialog(DanmakuSendViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
    }

    public string GetInputText() => InputBox.Text.Trim();
}
