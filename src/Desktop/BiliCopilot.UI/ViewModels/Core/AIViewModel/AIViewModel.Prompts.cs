// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Prompts;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// AI 视图模型.
/// </summary>
public sealed partial class AIViewModel
{
    private async Task InitializeVideoPromptsAsync()
    {
        var source = _videoView;
        var videoSummarizePrompt = await _promptTemplates.GetTemplateAsync(AIPromptKind.VideoSummary);
        var videoEvaluationPrompt = await _promptTemplates.GetTemplateAsync(AIPromptKind.VideoEvaluation);

        var videoSummaryItem = new AIQuickItemViewModel(
            FluentIcons.Common.Symbol.TextBulletListSquareSparkle,
            ResourceToolkit.GetLocalizedString(StringNames.VideoSummarize),
            "根据视频字幕及简介总结视频内容",
            "总结《{0}》的内容",
            videoSummarizePrompt,
            SummaryVideoAsync);
        var videoEvaluationItem = new AIQuickItemViewModel(
            FluentIcons.Common.Symbol.ChatSparkle,
            ResourceToolkit.GetLocalizedString(StringNames.VideoEvaluation),
            "根据视频内容及热门评论评价视频",
            "评价《{0}》的内容",
            videoEvaluationPrompt,
            EvaluateVideoAsync);
        if (ReferenceEquals(source, _videoView))
        {
            QuickItems = [videoSummaryItem, videoEvaluationItem];
        }
    }

    private async Task InitializeArticlePromptsAsync()
    {
        var source = _articleDetail;
        var articleSummarizePrompt = await _promptTemplates.GetTemplateAsync(AIPromptKind.ArticleSummary);
        var articleEvaluationPrompt = await _promptTemplates.GetTemplateAsync(AIPromptKind.ArticleEvaluation);

        var articleSummaryItem = new AIQuickItemViewModel(
            FluentIcons.Common.Symbol.DocumentOnePageSparkle,
            "文章总结",
            "根据文章内容总结文章",
            "总结《{0}》的内容",
            articleSummarizePrompt,
            SummaryArticleAsync);
        var articleEvaluationItem = new AIQuickItemViewModel(
            FluentIcons.Common.Symbol.ChatSparkle,
            "文章评价",
            "根据文章内容及热门评论评价文章",
            "评价《{0}》的内容",
            articleEvaluationPrompt,
            EvaluateArticleAsync);
        if (ReferenceEquals(source, _articleDetail))
        {
            QuickItems = [articleSummaryItem, articleEvaluationItem];
        }
    }

    private void InitOtherPrompts(AIQuickItemViewModel? currentPrompt)
    {
        _currentPrompt = currentPrompt;
        var morePrompts = QuickItems.Where(p => p != currentPrompt).ToList();
        morePrompts.Insert(0, GetCustomQuestionQuickItem());
        MorePrompts = morePrompts;
    }

    private AIQuickItemViewModel GetCustomQuestionQuickItem()
    {
        return new AIQuickItemViewModel(
            FluentIcons.Common.Symbol.Question,
            "举手提问",
            default,
            default,
            default,
            _ =>
            {
                Discard();
                return Task.CompletedTask;
            });
    }
}
