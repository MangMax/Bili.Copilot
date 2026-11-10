// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Automation;
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
    private bool _expanded;

    public static readonly DependencyProperty ExpandInlineProperty =
        DependencyProperty.Register(nameof(ExpandInline), typeof(bool), typeof(EmoteTextBlock), new PropertyMetadata(false));

    /// <summary>Expand long dynamic text in place instead of opening the comment overflow flyout.</summary>
    public bool ExpandInline
    {
        get => (bool)GetValue(ExpandInlineProperty);
        set => SetValue(ExpandInlineProperty, value);
    }

    /// <summary>Optional fixed inline-image height; NaN preserves the intrinsic image layout.</summary>
    public static readonly DependencyProperty EmoteHeightProperty =
        DependencyProperty.Register(nameof(EmoteHeight), typeof(double), typeof(EmoteTextBlock), new PropertyMetadata(double.NaN));

    /// <summary>Gets or sets a reserved inline-image height.</summary>
    public double EmoteHeight
    {
        get => (double)GetValue(EmoteHeightProperty);
        set => SetValue(EmoteHeightProperty, value);
    }

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

    /// <summary>Supplies a context menu for both the inline and expanded selectable text.</summary>
    public void SetTextContextFlyout(MenuFlyout flyout)
    {
        RichBlock.ContextFlyout = flyout;
        FlyoutRichBlock.ContextFlyout = flyout;
        RichBlock.SelectionFlyout = null;
        FlyoutRichBlock.SelectionFlyout = null;
    }

    protected override void OnControlUnloaded()
        => Gallery?.ItemsSource = default;

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var instance = d as EmoteTextBlock;
        instance._expanded = false;
        if (instance.RichBlock is not null) instance.RichBlock.MaxLines = instance.MaxLines;
        instance.InitializeContent();

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

        if (Text?.Pictures?.Count > 0)
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
                        // Reserve the inline box before decoding; image arrival must not change line height.
                        var width = !double.IsNaN(EmoteHeight) && emoji.Width > 0 && emoji.Height > 0
                            ? Math.Clamp(EmoteHeight * emoji.Width / emoji.Height, 16d, 96d)
                            : 20d;
                        var img = new Image
                        {
                            Width = width,
                            Height = EmoteHeight,
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(2, 0, 2, -4),
                        };
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
    {
        if (!ExpandInline)
        {
            TimestampTextPresenter.Append(para, content, SeekCommand);
            return;
        }

        var position = 0;
        foreach (Match match in MomentLinkRegex().Matches(content))
        {
            TimestampTextPresenter.AppendLines(para, content[position..match.Index], SeekCommand);
            var keyword = match.Value.Trim('#', '@');
            var scope = match.Value[0] == '@' ? "upuser" : "all";
            var link = new Hyperlink { NavigateUri = new Uri($"https://search.bilibili.com/{scope}?keyword={Uri.EscapeDataString(keyword)}") };
            link.Inlines.Add(new Run { Text = match.Value });
            para.Inlines.Add(link);
            position = match.Index + match.Length;
        }
        TimestampTextPresenter.AppendLines(para, content[position..], SeekCommand);
    }

    [GeneratedRegex(@"#[^#\r\n]+#|@[\p{L}\p{N}_-]+")]
    private static partial Regex MomentLinkRegex();

    private void CheckTextTrim()
    {
        if (ExpandInline)
        {
            OverflowButton.Flyout = null;
            var label = ResourceToolkit.GetLocalizedString(_expanded ? StringNames.CollapseMomentText : StringNames.ExpandMomentText);
            OverflowButton.Content = label;
            AutomationProperties.SetName(OverflowButton, label);
        }
        OverflowButton.Visibility = RichBlock.IsTextTrimmed || (ExpandInline && _expanded) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOverflowClick(object sender, RoutedEventArgs args)
    {
        if (!ExpandInline) return;
        _expanded = !_expanded;
        RichBlock.MaxLines = _expanded ? 0 : MaxLines;
        CheckTextTrim();
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
