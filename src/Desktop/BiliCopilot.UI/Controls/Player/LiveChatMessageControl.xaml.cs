// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// 单条聊天消息控件：直接以纯逻辑条目 <see cref="LiveChatMessageItem"/> 为视图模型。
/// </summary>
/// <remarks>
/// 正文需要把 [表情] 渲染成图片，纯 XAML 的 Run 做不到，因此这里用 RichTextBlock
/// 按分段重建段落；徽标仍走 XAML 绑定。
/// </remarks>
public sealed partial class LiveChatMessageControl : UserControl
{
    /// <summary>
    /// <see cref="ViewModel"/> 依赖属性.
    /// </summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(LiveChatMessageItem),
            typeof(LiveChatMessageControl),
            new PropertyMetadata(default, OnViewModelChanged));

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveChatMessageControl"/> class.
    /// </summary>
    public LiveChatMessageControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderContent();
    }

    /// <summary>
    /// Gets or sets 聊天条目.
    /// </summary>
    public LiveChatMessageItem ViewModel
    {
        get => (LiveChatMessageItem)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LiveChatMessageControl)d).RenderContent();

    private void RenderContent()
    {
        if (ViewModel is null || ContentBlock is null)
        {
            return;
        }

        var paragraph = new Paragraph();
        if (ViewModel.IsEnterRoom)
        {
            paragraph.Inlines.Add(new Run
            {
                Text = ViewModel.UserName ?? string.Empty,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = ViewModel.NameBrush,
            });
            paragraph.Inlines.Add(new Run { Text = " 进入直播间" });
            ContentBlock.Blocks.Clear();
            ContentBlock.Blocks.Add(paragraph);
            return;
        }

        var name = new Run
        {
            Text = (ViewModel.UserName ?? string.Empty) + "：",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        if (ViewModel.NameBrush is { } nameBrush)
        {
            name.Foreground = nameBrush;
        }

        paragraph.Inlines.Add(name);
        foreach (var segment in ViewModel.Segments)
        {
            if (!segment.IsEmote)
            {
                paragraph.Inlines.Add(new Run { Text = segment.Text });
                continue;
            }

            var url = segment.EmoteUrl!;
            if (url.StartsWith("//", StringComparison.Ordinal))
            {
                url = "https:" + url;
            }

            var image = new Image
            {
                Width = segment.EmoteSize,
                Height = segment.EmoteSize,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                Source = new BitmapImage(new Uri(url)),
            };
            paragraph.Inlines.Add(new InlineUIContainer { Child = image });
        }

        ContentBlock.Blocks.Clear();
        ContentBlock.Blocks.Add(paragraph);
    }
}
