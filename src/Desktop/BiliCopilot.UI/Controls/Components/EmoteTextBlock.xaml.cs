// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.WinUIKernel.Share.Base;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 表情富文本.
/// </summary>
public sealed partial class EmoteTextBlock : LayoutUserControlBase
{
    /// <summary>
    /// <see cref="MaxLines"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty MaxLinesProperty =
        DependencyProperty.Register(nameof(MaxLines), typeof(int), typeof(EmoteTextBlock), new PropertyMetadata(4));

    /// <summary>
    /// <see cref="Text"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(EmoteText), typeof(EmoteTextBlock), new PropertyMetadata(default, new PropertyChangedCallback(OnTextChanged)));

    /// <summary>
    /// <see cref="SeekCommand"/> 的依赖属性.
    /// </summary>
    /// <remarks>
    /// 用于把正文里的 <c>mm:ss</c> 时间戳渲染成可点击链接，参数为跳转秒数.
    /// </remarks>
    public static readonly DependencyProperty SeekCommandProperty =
        DependencyProperty.Register(nameof(SeekCommand), typeof(ICommand), typeof(EmoteTextBlock), new PropertyMetadata(default));

    /// <summary>
    /// Initializes a new instance of the <see cref="EmoteTextBlock"/> class.
    /// </summary>
    public EmoteTextBlock() => InitializeComponent();

    /// <summary>
    /// 最大行数.
    /// </summary>
    public int MaxLines
    {
        get => (int)GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    /// <summary>
    /// 输入的文本.
    /// </summary>
    public EmoteText Text
    {
        get => (EmoteText)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// 时间戳点击后执行跳转的命令，参数为秒数.
    /// </summary>
    public ICommand? SeekCommand
    {
        get => (ICommand?)GetValue(SeekCommandProperty);
        set => SetValue(SeekCommandProperty, value);
    }

    protected override void OnControlUnloaded()
        => Gallery?.ItemsSource = default;

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var instance = d as EmoteTextBlock;
        if (e.NewValue != null)
        {
            instance.InitializeContent();
        }

        instance.FlyoutRichBlock?.Blocks?.Clear();
    }

    private void InitializeContent()
    {
        if (RichBlock != null)
        {
            RichBlock.Blocks.Clear();
            Paragraph para = null;
            if (Text != null)
            {
                para = ParseText();
            }

            if (para != null)
            {
                RichBlock.Visibility = Visibility.Visible;
                RichBlock.Blocks.Add(para);
            }
            else
            {
                RichBlock.Visibility = Visibility.Collapsed;
            }

            CheckTextTrim();
        }

        if (Text.Pictures?.Count > 0)
        {
            var gallery = FindName("Gallery") as ItemsRepeater;
            gallery.ItemsSource = Text.Pictures;
            gallery.Visibility = Visibility.Visible;
        }
        else if (Gallery is not null)
        {
            Gallery.ItemsSource = default;
            Gallery.Visibility = Visibility.Collapsed;
        }
    }

    private void OnTextTrimChanged(RichTextBlock sender, IsTextTrimmedChangedEventArgs args)
        => CheckTextTrim();

    private Paragraph? ParseText()
    {
        if (string.IsNullOrEmpty(Text.Text))
        {
            return default;
        }

        var text = Text.Text;
        var emotes = Text.Emotes;
        var para = new Paragraph();

        if (emotes != null && emotes.Count > 0)
        {
            // 有表情存在，进行处理.
            var emojiRegex = new Regex(@"(\[.*?\])");
            var splitContents = emojiRegex.Split(text).Where(p => p.Length > 0).ToArray();
            foreach (var content in splitContents)
            {
                if (emojiRegex.IsMatch(content))
                {
                    _ = emotes.TryGetValue(content, out var emoji);
                    if (emoji != null && emoji.Uri is not null)
                    {
                        var inlineCon = new InlineUIContainer();
                        var img = new Image() { Width = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, -4) };
                        var bitmap = new BitmapImage(emoji.Uri) { DecodePixelWidth = 40 };
                        img.Source = bitmap;
                        inlineCon.Child = img;
                        para.Inlines.Add(inlineCon);
                    }
                    else
                    {
                        AppendInlineText(para, content);
                    }
                }
                else
                {
                    AppendInlineText(para, content);
                }
            }
        }
        else
        {
            AppendInlineText(para, text);
        }

        return para;
    }

    /// <summary>
    /// 把一段纯文本写入段落，其中的时间戳由共享呈现器渲染为可点击链接.
    /// </summary>
    /// <param name="para">目标段落.</param>
    /// <param name="content">文本内容.</param>
    private void AppendInlineText(Paragraph para, string content)
        => TimestampTextPresenter.Append(para, content, SeekCommand);

    private void CheckTextTrim()
    {
        OverflowButton.Visibility = RichBlock.IsTextTrimmed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnImageTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        var image = (sender as FrameworkElement).DataContext as BiliImage;
        if (image is not null)
        {
            new GalleryWindow(image, [.. Text.Pictures]).Activate();
        }
    }

    private void OnOverflowFlyoutOpened(object sender, object e)
    {
        if (FlyoutRichBlock.Blocks.Count > 0)
        {
            return;
        }

        var para = ParseText();
        if (para != null)
        {
            FlyoutRichBlock.Blocks.Add(para);
        }
    }
}
