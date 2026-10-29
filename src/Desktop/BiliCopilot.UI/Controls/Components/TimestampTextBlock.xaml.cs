// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Documents;
using System.Windows.Input;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 纯文本时间戳块.
/// </summary>
/// <remarks>
/// 通过共享呈现器把纯文本写入 <see cref="RichTextBlock"/>，其中的时间戳呈现为可点击链接；
/// 文本可选择、自动换行、保留原有换行，且默认不截断.
/// </remarks>
public sealed partial class TimestampTextBlock : UserControl
{
    /// <summary>
    /// <see cref="Text"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(TimestampTextBlock), new PropertyMetadata(default, OnContentChanged));

    /// <summary>
    /// <see cref="SeekCommand"/> 的依赖属性.
    /// </summary>
    /// <remarks>
    /// 用于把正文里的时间戳渲染成可点击链接，参数为跳转秒数.
    /// </remarks>
    public static readonly DependencyProperty SeekCommandProperty =
        DependencyProperty.Register(nameof(SeekCommand), typeof(ICommand), typeof(TimestampTextBlock), new PropertyMetadata(default, OnContentChanged));

    /// <summary>
    /// Initializes a new instance of the <see cref="TimestampTextBlock"/> class.
    /// </summary>
    public TimestampTextBlock() => InitializeComponent();

    /// <summary>
    /// 待呈现的文本.
    /// </summary>
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
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

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TimestampTextBlock)d).Render();

    private void Render()
    {
        if (RichBlock is null)
        {
            return;
        }

        RichBlock.Blocks.Clear();
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var paragraph = new Paragraph();

        // RichTextBlock 不会把 Run 里的换行符渲染成换行，这里显式插入 LineBreak.
        var lines = Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            TimestampTextPresenter.Append(paragraph, lines[i], SeekCommand);
        }

        RichBlock.Blocks.Add(paragraph);
    }
}
