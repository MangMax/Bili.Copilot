// Copyright (c) Bili Copilot. All rights reserved.

using Richasy.BiliKernel.Models.Article;

namespace BiliCopilot.UI.Controls.Article;

public sealed partial class ArticleReader
{
    private readonly Func<Task<(string Html, string Css, string Theme)>> _readTemplate;
    private readonly Action<string> _navigate;
    private readonly Action _clear;
    private int _injectionGeneration;

    internal ArticleReader(
        Func<Task<(string Html, string Css, string Theme)>> readTemplate,
        Action<string> navigate,
        Action clear)
    {
        _readTemplate = readTemplate;
        _navigate = navigate;
        _clear = clear;
    }

    /// <summary>
    /// 加载文章内容.
    /// </summary>
    public async Task LoadContentAsync(
        ArticleDetail article,
        string fontFamily = "Segoe UI",
        double fontSize = 16,
        double lineHeight = 1.5,
        Func<bool>? isCurrent = null)
    {
        var generation = ++_injectionGeneration;
        var (localHtml, localCss, theme) = await _readTemplate();
        localCss = localCss.Replace("$FontFamily$", fontFamily)
            .Replace("$FontSize$", $"{fontSize}")
            .Replace("$LineHeight$", $"{lineHeight}");
        var html = localHtml.Replace("$theme$", theme.ToLower())
            .Replace("$style$", localCss)
            .Replace("$title$", article.Identifier.Title)
            .Replace("$author$", article.Author.Profile.User.Name)
            .Replace("$date$", article.PublishTime.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("$view$", FormatViews(article))
            .Replace("$body$", article.HtmlContent);
        // Template I/O can finish after another article or page owns the reader.
        if (generation == _injectionGeneration && (isCurrent?.Invoke() ?? true))
        {
            _navigate(html);
        }
    }

    /// <summary>
    /// 清除内容.
    /// </summary>
    public void ClearContent()
    {
        ++_injectionGeneration;
        _clear();
    }
}
