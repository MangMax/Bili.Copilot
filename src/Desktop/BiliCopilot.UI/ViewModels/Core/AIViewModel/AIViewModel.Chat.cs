// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Richasy.AgentKernel;
using Richasy.AgentKernel.Chat;
using Richasy.BiliKernel.Bili.Comment;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Base;
using RichasyKernel;
using System.Text.RegularExpressions;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// AI 视图模型.
/// </summary>
public sealed partial class AIViewModel
{
    private async Task SummaryVideoAsync(AIQuickItemViewModel source)
    {
        using var generation = BeginGeneration();
        var token = generation.Token;
        try
        {
            Erase();
            InitOtherPrompts(source);
            RequestText = string.Format(source.RequestTemplate, _videoView.Information.Identifier.Title);
            var promptTemplate = source.Prompt;
            IsGenerating = true;
            var subtitleText = await GetVideoSubtitleAsync(token);
            EnsureCurrentGeneration(token);
            subtitleText ??= "无字幕";

            // 生成总结.
            ProgressTip = ResourceToolkit.GetLocalizedString(StringNames.Summarizing);
            var prompt = promptTemplate
                .Replace("{title}", _videoView.Information.Identifier.Title)
                .Replace("{description}", _videoView.Information.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description))
                .Replace("{subtitle}", subtitleText);
            await SendMessageAsync(prompt, token);
            EnsureCurrentGeneration(token);
            ProgressTip = default;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (IsCurrentGeneration(token))
        {
            _logger.LogError(ex, "总结视频内容失败");
            ErrorMessage = ex.Message;
        }
        catch (Exception) when (!IsCurrentGeneration(token))
        {
        }
        finally
        {
            if (IsCurrentGeneration(token))
            {
                IsGenerating = false;
                ProgressTip = default;
                _generateCancellationTokenSource = null;
            }
        }
    }

    private async Task EvaluateVideoAsync(AIQuickItemViewModel source)
    {
        using var generation = BeginGeneration();
        var token = generation.Token;
        try
        {
            Erase();
            InitOtherPrompts(source);
            RequestText = string.Format(source.RequestTemplate, _videoView.Information.Identifier.Title);
            var promptTemplate = source.Prompt;
            IsGenerating = true;
            var subtitleText = await GetVideoSubtitleAsync(token);
            EnsureCurrentGeneration(token);
            subtitleText ??= "无字幕";
            var commentText = await GetHotCommentTextAsync(_videoView.Information.Identifier.Id, CommentTargetType.Video, token);
            EnsureCurrentGeneration(token);
            commentText ??= "无评论";

            // 生成总结.
            ProgressTip = ResourceToolkit.GetLocalizedString(StringNames.Generating);
            var prompt = promptTemplate
                .Replace("{title}", _videoView.Information.Identifier.Title)
                .Replace("{description}", _videoView.Information.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description))
                .Replace("{subtitle}", subtitleText)
                .Replace("{comments}", commentText);
            await SendMessageAsync(prompt, token);
            EnsureCurrentGeneration(token);
            ProgressTip = default;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (IsCurrentGeneration(token))
        {
            _logger.LogError(ex, "评价视频内容失败");
            ErrorMessage = ex.Message;
        }
        catch (Exception) when (!IsCurrentGeneration(token))
        {
        }
        finally
        {
            if (IsCurrentGeneration(token))
            {
                IsGenerating = false;
                ProgressTip = default;
                _generateCancellationTokenSource = null;
            }
        }
    }

    private async Task AskVideoQuestionAsync(string question)
    {
        if (string.IsNullOrEmpty(question))
        {
            return;
        }

        using var generation = BeginGeneration();
        var token = generation.Token;
        try
        {
            Erase();
            InitOtherPrompts(default);
            RequestText = question;
            _lastQuestion = question;
            var promptTemplate = PromptConstants.VideoQuestionPrompt;
            IsGenerating = true;
            var subtitleText = await GetVideoSubtitleAsync(token);
            EnsureCurrentGeneration(token);
            subtitleText ??= "无字幕";

            // 生成总结.
            ProgressTip = ResourceToolkit.GetLocalizedString(StringNames.Generating);
            var prompt = promptTemplate
                .Replace("{title}", _videoView.Information.Identifier.Title)
                .Replace("{description}", _videoView.Information.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description))
                .Replace("{subtitle}", subtitleText)
                .Replace("{question}", question);
            await SendMessageAsync(prompt, token);
            EnsureCurrentGeneration(token);
            ProgressTip = default;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (IsCurrentGeneration(token))
        {
            _logger.LogError(ex, "回答问题失败");
            ErrorMessage = ex.Message;
        }
        catch (Exception) when (!IsCurrentGeneration(token))
        {
        }
        finally
        {
            if (IsCurrentGeneration(token))
            {
                IsGenerating = false;
                ProgressTip = default;
                _generateCancellationTokenSource = null;
            }
        }
    }

    private async Task SummaryArticleAsync(AIQuickItemViewModel source)
    {
        using var generation = BeginGeneration();
        var token = generation.Token;
        try
        {
            Erase();
            InitOtherPrompts(source);
            RequestText = string.Format(source.RequestTemplate, _articleDetail.Identifier.Title);
            var promptTemplate = source.Prompt;
            IsGenerating = true;
            var articleContent = GetArticlePlainText();

            // 生成总结.
            ProgressTip = ResourceToolkit.GetLocalizedString(StringNames.Summarizing);
            var prompt = promptTemplate
                .Replace("{title}", _articleDetail.Identifier.Title)
                .Replace("{content}", articleContent);
            await SendMessageAsync(prompt, token);
            EnsureCurrentGeneration(token);
            ProgressTip = default;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (IsCurrentGeneration(token))
        {
            _logger.LogError(ex, "总结文章内容失败");
            ErrorMessage = ex.Message;
        }
        catch (Exception) when (!IsCurrentGeneration(token))
        {
        }
        finally
        {
            if (IsCurrentGeneration(token))
            {
                IsGenerating = false;
                ProgressTip = default;
                _generateCancellationTokenSource = null;
            }
        }
    }

    private async Task EvaluateArticleAsync(AIQuickItemViewModel source)
    {
        using var generation = BeginGeneration();
        var token = generation.Token;
        try
        {
            Erase();
            InitOtherPrompts(source);
            RequestText = string.Format(source.RequestTemplate, _articleDetail.Identifier.Title);
            var promptTemplate = source.Prompt;
            IsGenerating = true;
            var articleContent = GetArticlePlainText();

            var commentText = await GetHotCommentTextAsync(_articleDetail.Identifier.Id, CommentTargetType.Article, token);
            EnsureCurrentGeneration(token);
            commentText ??= "无评论";

            // 生成总结.
            ProgressTip = ResourceToolkit.GetLocalizedString(StringNames.Generating);
            var prompt = promptTemplate
                .Replace("{title}", _articleDetail.Identifier.Title)
                .Replace("{content}", articleContent)
                .Replace("{comments}", commentText);
            await SendMessageAsync(prompt, token);
            EnsureCurrentGeneration(token);
            ProgressTip = default;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (IsCurrentGeneration(token))
        {
            _logger.LogError(ex, "评价文章内容失败");
            ErrorMessage = ex.Message;
        }
        catch (Exception) when (!IsCurrentGeneration(token))
        {
        }
        finally
        {
            if (IsCurrentGeneration(token))
            {
                IsGenerating = false;
                ProgressTip = default;
                _generateCancellationTokenSource = null;
            }
        }
    }

    private async Task AskArticleQuestionAsync(string question)
    {
        if (string.IsNullOrEmpty(question))
        {
            return;
        }

        using var generation = BeginGeneration();
        var token = generation.Token;
        try
        {
            Erase();
            InitOtherPrompts(default);
            RequestText = question;
            _lastQuestion = question;
            var promptTemplate = PromptConstants.ArticleQuestionPrompt;
            IsGenerating = true;
            var articleContent = GetArticlePlainText();

            // 生成总结.
            ProgressTip = ResourceToolkit.GetLocalizedString(StringNames.Generating);
            var prompt = promptTemplate
                .Replace("{title}", _articleDetail.Identifier.Title)
                .Replace("{content}", articleContent)
                .Replace("{question}", question);
            await SendMessageAsync(prompt, token);
            EnsureCurrentGeneration(token);
            ProgressTip = default;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (IsCurrentGeneration(token))
        {
            _logger.LogError(ex, "回答问题失败");
            ErrorMessage = ex.Message;
        }
        catch (Exception) when (!IsCurrentGeneration(token))
        {
        }
        finally
        {
            if (IsCurrentGeneration(token))
            {
                IsGenerating = false;
                ProgressTip = default;
                _generateCancellationTokenSource = null;
            }
        }
    }

    private async Task<string?> GetVideoSubtitleAsync(CancellationToken token)
    {
        if (_subtitles is null)
        {
            var service = this.Get<ISubtitleService>();

            // 1. 先获取字幕元数据.
            ProgressTip = ResourceToolkit.GetLocalizedString(StringNames.GettingSubtitleMeta);
            var metas = await service.GetSubtitleMetasAsync(_videoView.Information.Identifier.Id, _videoPart.Identifier.Id, token);
            EnsureCurrentGeneration(token);
            if (metas is null || metas.Count == 0)
            {
                ErrorMessage = ResourceToolkit.GetLocalizedString(StringNames.NoSubtitleAvailable);
                return default;
            }

            // 2. 选择字幕元数据.
            ProgressTip = ResourceToolkit.GetLocalizedString(StringNames.GettingSubtitleInformation);
            var preferredIndex = SubtitlePreference.SelectPreferredIndex(metas, p => p.LanguageName, p => p.IsAI);
            var meta = metas[preferredIndex];
            if (!IsCurrentGeneration(token))
            {
                return default;
            }

            var subtitles = await service.GetSubtitleDetailAsync(meta, token);
            EnsureCurrentGeneration(token);
            _subtitles = [.. subtitles];
        }

        return string.Join(Environment.NewLine, _subtitles.Select(p => $"{AppToolkit.FormatDuration(TimeSpan.FromSeconds(p.StartPosition))} - {AppToolkit.FormatDuration(TimeSpan.FromSeconds(p.EndPosition))}: {p.Content}"));
    }

    private async Task<string?> GetHotCommentTextAsync(string id, CommentTargetType target, CancellationToken token)
    {
        // 获取评论.
        ProgressTip = ResourceToolkit.GetLocalizedString(StringNames.GettingHotComments);
        var commentService = this.Get<ICommentService>();
        var comments = await commentService.GetCommentsAsync(id, target, CommentSortType.Hot, cancellationToken: token);
        if (!IsCurrentGeneration(token))
        {
            return default;
        }

        var commentText = string.Empty;
        if (comments?.Comments.Count > 0)
        {
            foreach (var item in comments.Comments.Select(p => p.Content.Text + "\n------"))
            {
                if (commentText.Length + item.Length > 800)
                {
                    break;
                }

                commentText += item;
            }
        }

        return commentText;
    }

    private async Task SendMessageAsync(string prompt, CancellationToken token)
    {
        EnsureCurrentGeneration(token);
        var session = new StreamingResponseSession(
            callback => _ = ScheduleStreamingAsync(callback, token),
            (text, final) =>
            {
                if (!IsCurrentGeneration(token))
                {
                    return;
                }

                if (final)
                {
                    TempResult = string.Empty;
                    FinalResult = text.Trim();
                }
                else
                {
                    TempResult = text;
                }
            });
        using var registration = token.Register(session.Cancel);
        try
        {
            var useStreaming = SettingsToolkit.ReadLocalSetting(SettingNames.IsAIStreamingResponse, true);
            var result = await SendMessageInternalAsync(SelectedService.ProviderType, prompt,
                useStreaming ? session.Append : null, token);
            EnsureCurrentGeneration(token);
            if (!useStreaming)
            {
                session.Append(result);
            }

            session.Complete();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && IsCurrentGeneration(token))
        {
            TempResult = string.Empty;
            this.Get<AppViewModel>().ShowTipCommand.Execute((ex.Message, InfoType.Error));
            throw;
        }
        finally
        {
            session.Cancel();
        }
    }

    private async Task ScheduleStreamingAsync(Action callback, CancellationToken token)
    {
        try
        {
            await Task.Delay(75, token).ConfigureAwait(false);
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (IsCurrentGeneration(token))
                {
                    callback();
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<string> SendMessageInternalAsync(
        ChatProviderType type,
        string? message,
        Action<string>? streamingAction = null,
        CancellationToken cancellationToken = default)
    {
        var model = SelectedModel;
        var messages = new List<ChatMessage>();
        var options = new ChatOptions
        {
            ModelId = model.Id,
        };
        var config = await this.Get<IChatConfigManager>().GetServiceConfigAsync(type, model.Data);
        cancellationToken.ThrowIfCancellationRequested();
        var service = GlobalDependencies.Kernel.GetRequiredService<IChatService>(type.ToString());
        service.Initialize(config);
        messages.Add(new(ChatRole.User, message));
        var responseContent = string.Empty;
        var hasStreamingContent = false;
        try
        {
            if (streamingAction is null)
            {
                var response = await service.Client!.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
                responseContent = response.Text ?? string.Empty;
            }
            else
            {
                await foreach (var partialResponse in service.Client!.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
                {
                    if (!string.IsNullOrEmpty(partialResponse.Text))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        hasStreamingContent = true;
                        streamingAction.Invoke(partialResponse.Text);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{type} | {model.Id} 发生错误");
            throw;
        }

        return hasStreamingContent || !string.IsNullOrEmpty(responseContent) ? responseContent : throw new KernelException($"{type} | {model.Id} 返回空响应，具体错误请查看日志");
    }

    private string GetArticlePlainText()
    {
        var html = _articleDetail.HtmlContent;
        html = Regex.Replace(html, "<[^>]+>", string.Empty);
        html = Regex.Replace(html, @"\s+", " ");
        if (string.IsNullOrEmpty(html.Trim()))
        {
            throw new Exception(ResourceToolkit.GetLocalizedString(StringNames.ArticleContentEmpty));
        }

        return html;
    }

    private CancellationTokenSource BeginGeneration()
    {
        _generateCancellationTokenSource?.Cancel();
        var generation = new CancellationTokenSource();
        _generateCancellationTokenSource = generation;
        return generation;
    }

    private bool IsCurrentGeneration(CancellationToken token)
        => !token.IsCancellationRequested && _generateCancellationTokenSource?.Token == token;

    private void EnsureCurrentGeneration(CancellationToken token)
    {
        if (!IsCurrentGeneration(token))
        {
            throw new OperationCanceledException(token);
        }
    }
}
