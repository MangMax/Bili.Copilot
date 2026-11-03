// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Context;
using BiliCopilot.UI.AI.Conversation;
using BiliCopilot.UI.AI.Prompts;
using BiliCopilot.UI.ViewModels.Items;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// AI 视图模型.
/// </summary>
public sealed partial class AIViewModel
{
    private async Task SummaryVideoAsync(AIQuickItemViewModel source)
    {
        if (_videoView is null || _videoPart is null)
        {
            return;
        }

        PrepareGeneration(source);
        await _orchestrator.RunAsync(new AIConversationRequest
        {
            Kind = AIPromptKind.VideoSummary,
            RequestText = string.Format(source.RequestTemplate, _videoView.Information.Identifier.Title),
            Template = source.Prompt,
            Source = CreateVideoSource(),
        });
    }

    private async Task EvaluateVideoAsync(AIQuickItemViewModel source)
    {
        if (_videoView is null || _videoPart is null)
        {
            return;
        }

        PrepareGeneration(source);
        await _orchestrator.RunAsync(new AIConversationRequest
        {
            Kind = AIPromptKind.VideoEvaluation,
            RequestText = string.Format(source.RequestTemplate, _videoView.Information.Identifier.Title),
            Template = source.Prompt,
            Source = CreateVideoSource(),
        });
    }

    private async Task AskVideoQuestionAsync(string question)
    {
        if (string.IsNullOrWhiteSpace(question) || _videoView is null || _videoPart is null)
        {
            return;
        }

        PrepareGeneration(default);
        _lastQuestion = question;
        var template = await _promptTemplates.GetTemplateAsync(AIPromptKind.VideoQuestion);
        await _orchestrator.RunAsync(new AIConversationRequest
        {
            Kind = AIPromptKind.VideoQuestion,
            RequestText = question,
            Question = question,
            Template = template,
            Source = CreateVideoSource(),
        });
    }

    private async Task SummaryArticleAsync(AIQuickItemViewModel source)
    {
        if (_articleDetail is null)
        {
            return;
        }

        PrepareGeneration(source);
        await _orchestrator.RunAsync(new AIConversationRequest
        {
            Kind = AIPromptKind.ArticleSummary,
            RequestText = string.Format(source.RequestTemplate, _articleDetail.Identifier.Title),
            Template = source.Prompt,
            Source = CreateArticleSource(),
        });
    }

    private async Task EvaluateArticleAsync(AIQuickItemViewModel source)
    {
        if (_articleDetail is null)
        {
            return;
        }

        PrepareGeneration(source);
        await _orchestrator.RunAsync(new AIConversationRequest
        {
            Kind = AIPromptKind.ArticleEvaluation,
            RequestText = string.Format(source.RequestTemplate, _articleDetail.Identifier.Title),
            Template = source.Prompt,
            Source = CreateArticleSource(),
        });
    }

    private async Task AskArticleQuestionAsync(string question)
    {
        if (string.IsNullOrWhiteSpace(question) || _articleDetail is null)
        {
            return;
        }

        PrepareGeneration(default);
        _lastQuestion = question;
        var template = await _promptTemplates.GetTemplateAsync(AIPromptKind.ArticleQuestion);
        await _orchestrator.RunAsync(new AIConversationRequest
        {
            Kind = AIPromptKind.ArticleQuestion,
            RequestText = question,
            Question = question,
            Template = template,
            Source = CreateArticleSource(),
        });
    }

    private void PrepareGeneration(AIQuickItemViewModel? source)
    {
        _lastQuestion = string.Empty;
        InitOtherPrompts(source);
    }

    private AIConversationSource CreateVideoSource()
        => new()
        {
            Target = AIContextTarget.Video,
            Id = _videoView.Information.Identifier.Id,
            PartId = _videoPart.Identifier.Id,
            Title = _videoView.Information.Identifier.Title,
            Description = _videoView.Information.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description),
        };

    private AIConversationSource CreateArticleSource()
        => new()
        {
            Target = AIContextTarget.Article,
            Id = _articleDetail.Identifier.Id,
            Title = _articleDetail.Identifier.Title,
            HtmlContent = _articleDetail.HtmlContent,
        };
}
