// Copyright (c) Bili Copilot. All rights reserved.

using System.Text.RegularExpressions;

namespace BiliCopilot.UI.AI.Prompts;

/// <summary>
/// 把变量填入提示词模板的纯函数，不依赖 UI 与提示词文件.
/// </summary>
/// <remarks>
/// 模板只扫描一次：变量值里出现的 <c>{title}</c> 等字面量不会被再次替换，
/// 未识别的占位符保持原样.
/// </remarks>
public static partial class AIPromptComposer
{
    /// <summary>
    /// 没有可用字幕时填入的缺省文案.
    /// </summary>
    public const string NoSubtitleText = "无字幕";

    /// <summary>
    /// 没有可用评论时填入的缺省文案.
    /// </summary>
    public const string NoCommentText = "无评论";

    /// <summary>
    /// 按提示词类型把变量填入模板.
    /// </summary>
    /// <param name="kind">提示词类型，决定哪些占位符会被识别.</param>
    /// <param name="template">提示词模板，未识别的占位符原样保留.</param>
    /// <param name="variables">变量集合，为空时视为全部缺省.</param>
    /// <returns>填充后的提示词.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> 为 <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">提问类提示词没有提供问题内容.</exception>
    public static string Build(AIPromptKind kind, string template, AIPromptVariables? variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        variables ??= new AIPromptVariables();

        var isQuestion = kind is AIPromptKind.VideoQuestion or AIPromptKind.ArticleQuestion;
        if (isQuestion && string.IsNullOrWhiteSpace(variables.Question))
        {
            throw new ArgumentException("提问类提示词必须提供问题内容。", nameof(variables));
        }

        // 只登记该类型适用的占位符：不适用的已知占位符按未识别处理，原样保留。
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (kind is AIPromptKind.VideoSummary or AIPromptKind.VideoEvaluation or AIPromptKind.VideoQuestion)
        {
            values["title"] = variables.Title ?? string.Empty;
            values["description"] = variables.Description ?? string.Empty;
            values["subtitle"] = string.IsNullOrWhiteSpace(variables.Subtitle) ? NoSubtitleText : variables.Subtitle;
        }
        else
        {
            values["title"] = variables.Title ?? string.Empty;
            values["content"] = variables.ArticleContent ?? string.Empty;
        }

        if (kind is AIPromptKind.VideoEvaluation or AIPromptKind.ArticleEvaluation)
        {
            values["comments"] = string.IsNullOrWhiteSpace(variables.Comments) ? NoCommentText : variables.Comments;
        }

        if (isQuestion)
        {
            values["question"] = variables.Question!;
        }

        return PlaceholderRegex().Replace(
            template,
            match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
    }

    [GeneratedRegex(@"\{([A-Za-z0-9_]+)\}", RegexOptions.None, 200)]
    private static partial Regex PlaceholderRegex();
}
