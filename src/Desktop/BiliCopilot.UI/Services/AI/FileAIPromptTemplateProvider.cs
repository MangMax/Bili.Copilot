// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Conversation;
using BiliCopilot.UI.AI.Prompts;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Richasy.WinUIKernel.Share.Toolkits;

namespace BiliCopilot.UI.Services.AI;

/// <summary>
/// 基于本地提示词文件与默认模板的模板来源适配器.
/// </summary>
/// <remarks>
/// 与原有行为一致：四个可编辑模板的文件缺失时写入默认模板，提问类提示词直接使用内置常量.
/// </remarks>
internal sealed class FileAIPromptTemplateProvider : IAIPromptTemplateProvider
{
    private const string PromptFolder = "Prompt";

    private static readonly Dictionary<AIPromptKind, PromptFile> PromptFiles = new()
    {
        [AIPromptKind.VideoSummary] = new("video_summarize.txt", PromptConstants.VideoSummaryPrompt),
        [AIPromptKind.VideoEvaluation] = new("video_evaluation.txt", PromptConstants.VideoEvaluationPrompt),
        [AIPromptKind.ArticleSummary] = new("article_summarize.txt", PromptConstants.ArticleSummaryPrompt),
        [AIPromptKind.ArticleEvaluation] = new("article_evaluation.txt", PromptConstants.ArticleEvaluationPrompt),
    };

    private readonly IFileToolkit _fileToolkit;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAIPromptTemplateProvider"/> class.
    /// </summary>
    /// <param name="fileToolkit">本地文件工具.</param>
    public FileAIPromptTemplateProvider(IFileToolkit fileToolkit)
    {
        ArgumentNullException.ThrowIfNull(fileToolkit);
        _fileToolkit = fileToolkit;
    }

    /// <inheritdoc/>
    public async Task<string> GetTemplateAsync(AIPromptKind kind, CancellationToken cancellationToken = default)
    {
        if (!PromptFiles.TryGetValue(kind, out var file))
        {
            return kind switch
            {
                AIPromptKind.VideoQuestion => PromptConstants.VideoQuestionPrompt,
                AIPromptKind.ArticleQuestion => PromptConstants.ArticleQuestionPrompt,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的提示词类型。"),
            };
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!FileToolkit.IsLocalDataExist(file.FileName, PromptFolder))
        {
            await _fileToolkit.WriteLocalDataAsync(file.FileName, file.DefaultText, default, PromptFolder);
        }

        return await _fileToolkit.ReadLocalDataAsync<string>(file.FileName, default, string.Empty, PromptFolder);
    }

    private sealed record PromptFile(string FileName, string DefaultText);
}
