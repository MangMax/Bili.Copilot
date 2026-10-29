// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Documents;
using System.Windows.Input;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 把文本中的时间戳呈现为可点击链接的共享辅助.
/// </summary>
/// <remarks>
/// 评论与视频简介都通过这里写入段落，链接样式与点击行为只保留一处实现.
/// </remarks>
internal static class TimestampTextPresenter
{
    /// <summary>
    /// 把一段文本写入段落，其中的时间戳渲染为可点击链接.
    /// </summary>
    /// <param name="paragraph">目标段落.</param>
    /// <param name="text">待呈现的文本，允许为 null.</param>
    /// <param name="seekCommand">点击时间戳时执行的跳转命令，参数为秒数.</param>
    public static void Append(Paragraph paragraph, string? text, ICommand? seekCommand)
        => Append(paragraph, TimestampText.Split(text), seekCommand);

    /// <summary>
    /// 按换行切分文本后写入段落，保留原有换行.
    /// </summary>
    /// <remarks>
    /// <see cref="RichTextBlock"/> 不会把 <see cref="Run"/> 里的换行符渲染成换行，
    /// 因此这里显式拆分文本并在行间插入 <see cref="LineBreak"/>.
    /// </remarks>
    /// <param name="paragraph">目标段落.</param>
    /// <param name="text">待呈现的文本，允许为 null.</param>
    /// <param name="seekCommand">点击时间戳时执行的跳转命令，参数为秒数.</param>
    public static void AppendLines(Paragraph paragraph, string? text, ICommand? seekCommand)
    {
        ArgumentNullException.ThrowIfNull(paragraph);

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            Append(paragraph, lines[i], seekCommand);
        }
    }

    /// <summary>
    /// 把已切分好的文本写入段落，其中的时间戳渲染为可点击链接.
    /// </summary>
    /// <param name="paragraph">目标段落.</param>
    /// <param name="segments">按顺序排列的文本段与时间戳段.</param>
    /// <param name="seekCommand">点击时间戳时执行的跳转命令，参数为秒数.</param>
    public static void Append(
        Paragraph paragraph,
        IReadOnlyList<TimestampTextSegment> segments,
        ICommand? seekCommand)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(segments);

        foreach (var segment in segments)
        {
            paragraph.Inlines.Add(CreateInline(segment, seekCommand));
        }
    }

    /// <summary>
    /// 创建单个文本段对应的内联元素.
    /// </summary>
    /// <param name="segment">文本段或时间戳段.</param>
    /// <param name="seekCommand">点击时间戳时执行的跳转命令，参数为秒数.</param>
    /// <returns>普通文本段返回 <see cref="Run"/>；时间戳段返回 <see cref="Hyperlink"/>.</returns>
    private static Inline CreateInline(TimestampTextSegment segment, ICommand? seekCommand)
    {
        ArgumentNullException.ThrowIfNull(segment);

        if (segment.Seconds is not double seconds || seekCommand is null)
        {
            return new Run { Text = segment.Text };
        }

        // 不设置 Foreground，沿用 Hyperlink 默认的主题强调色，浅色与深色主题切换时自动跟随.
        // WinUI 的 Hyperlink 默认已提供手形光标，无需额外的指针处理.
        var link = new Hyperlink
        {
            UnderlineStyle = UnderlineStyle.None,
        };
        link.Inlines.Add(new Run { Text = segment.Text });
        link.Click += (_, _) =>
        {
            if (seekCommand.CanExecute(seconds))
            {
                seekCommand.Execute(seconds);
            }
        };

        return link;
    }
}
