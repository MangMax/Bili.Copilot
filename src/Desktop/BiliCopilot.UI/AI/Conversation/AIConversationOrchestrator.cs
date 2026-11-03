// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Chat;
using BiliCopilot.UI.AI.Context;
using BiliCopilot.UI.AI.Prompts;

namespace BiliCopilot.UI.AI.Conversation;

/// <summary>
/// AI 对话编排服务：把上下文采集、提示词构建、配置快照与请求执行组合成一次请求。
/// </summary>
/// <remarks>
/// <para>
/// 六个入口共用同一份实现，由 <see cref="AIConversationRequest.Kind"/> 区分采集范围与提示词占位符；
/// 错误处理、代次隔离与取消只在这里写一次。
/// </para>
/// <para>
/// 状态变化通过 <see cref="StateChanged"/> 发布，并通过 <see cref="IAIConversationScheduler"/> 封送到界面线程；
/// 旧代次的迟到增量不会写入新代次，取消后不再提交结果。
/// </para>
/// </remarks>
public sealed class AIConversationOrchestrator
{
    private readonly AIContextCollector _contextCollector;
    private readonly AIChatRequestRunner _requestRunner;
    private readonly IAIConversationScheduler _scheduler;
    private readonly AIGenerationCoordinator _generationCoordinator = new();
    private readonly object _gate = new();

    private AIConversationSnapshot _state = new();
    private AIGeneration? _generation;
    private int _runId;

    internal AIConversationOrchestrator(
        AIContextCollector contextCollector,
        AIChatRequestRunner requestRunner,
        IAIConversationScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(contextCollector);
        ArgumentNullException.ThrowIfNull(requestRunner);
        ArgumentNullException.ThrowIfNull(scheduler);

        _contextCollector = contextCollector;
        _requestRunner = requestRunner;
        _scheduler = scheduler;
    }

    /// <summary>
    /// 在状态发生变化时触发；回调在调度器指定的线程上执行。
    /// </summary>
    public event EventHandler<AIConversationStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 获取当前状态快照。
    /// </summary>
    public AIConversationSnapshot State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// 执行一次请求；取消与失败不会向调用方抛出，统一记录在状态快照中。
    /// </summary>
    /// <param name="request">请求描述。</param>
    /// <returns>请求结束（完成、失败或取消）后的任务。</returns>
    public async Task RunAsync(AIConversationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        AIGeneration generation;
        int runId;
        lock (_gate)
        {
            runId = ++_runId;
            generation = _generationCoordinator.Begin();
            _generation = generation;
            _state = new AIConversationSnapshot
            {
                RequestText = request.RequestText,
                IsGenerating = true,
            };
            PublishState(runId);
        }

        var chatStarted = false;
        try
        {
            var contextRequest = new AIContextRequest
            {
                Target = request.Source.Target,
                Id = request.Source.Id,
                PartId = request.Source.PartId,
                Title = request.Source.Title,
                Description = request.Source.Description,
                HtmlContent = request.Source.HtmlContent,
                IncludeComments = IncludesComments(request.Kind),
            };
            var progress = new ContextStageProgress(stage => ApplyContextStage(runId, stage));
            var context = await _contextCollector
                .CollectAsync(contextRequest, progress, generation.Token)
                .ConfigureAwait(false);
            if (!IsCurrentRun(runId, generation))
            {
                return;
            }

            var warning = context.SubtitleStatus == AISubtitleStatus.Unavailable
                && request.Source.Target == AIContextTarget.Video
                ? AIConversationWarning.NoSubtitle
                : (AIConversationWarning?)null;
            var stage = IsSummary(request.Kind)
                ? AIConversationStage.Summarizing
                : AIConversationStage.Generating;
            Mutate(runId, snapshot => snapshot with
            {
                Warning = warning,
                Stage = stage,
            });

            var prompt = AIPromptComposer.Build(request.Kind, request.Template, CreateVariables(request, context));
            var target = new AIChatStreamTarget(
                callback => _scheduler.Schedule(callback, generation.Token),
                (text, isFinal) => ApplyStreamedText(runId, text, isFinal));
            chatStarted = true;
            await _requestRunner.RunAsync(generation, prompt, target).ConfigureAwait(false);
            if (!IsCurrentRun(runId, generation))
            {
                return;
            }

            Mutate(runId, snapshot => snapshot with
            {
                Stage = null,
                IsGenerating = false,
            });
        }
        catch (OperationCanceledException)
        {
            // 取消后由 Cancel 或新代次决定状态，这里不写回。
        }
        catch (Exception ex)
        {
            if (IsCurrentRun(runId, generation))
            {
                RecordFailure(runId, ex, chatStarted);
            }
        }
        finally
        {
            lock (_gate)
            {
                if (_runId == runId)
                {
                    _generation = null;
                }
            }

            generation.Dispose();
        }
    }

    /// <summary>
    /// 取消当前请求并清空状态；没有进行中的请求时仅清空状态。
    /// </summary>
    public void Cancel()
    {
        AIGeneration? generation;
        int runId;
        lock (_gate)
        {
            runId = ++_runId;
            generation = _generation;
            _generation = null;
            _state = new AIConversationSnapshot();
            PublishState(runId);
        }

        generation?.Cancel();
    }

    private static bool IncludesComments(AIPromptKind kind)
        => kind is AIPromptKind.VideoEvaluation or AIPromptKind.ArticleEvaluation;

    private static bool IsSummary(AIPromptKind kind)
        => kind is AIPromptKind.VideoSummary or AIPromptKind.ArticleSummary;

    private static AIPromptVariables CreateVariables(AIConversationRequest request, AISourceContext context)
        => new()
        {
            Title = context.Title,
            Description = context.Description,
            Subtitle = context.SubtitleText,
            Comments = context.CommentText,
            ArticleContent = context.ArticleText,
            Question = request.Question,
        };

    private static AIConversationStage MapStage(AIContextStage stage)
        => stage switch
        {
            AIContextStage.SubtitleMetadata => AIConversationStage.SubtitleMetadata,
            AIContextStage.SubtitleDetail => AIConversationStage.SubtitleDetail,
            AIContextStage.Comments => AIConversationStage.Comments,
            AIContextStage.ArticleContent => AIConversationStage.ArticleContent,
            _ => AIConversationStage.Generating,
        };

    private bool IsCurrentRun(int runId, AIGeneration generation)
    {
        lock (_gate)
        {
            return _runId == runId && generation.IsCurrent;
        }
    }

    private void ApplyContextStage(int runId, AIContextStage stage)
        => Mutate(runId, snapshot => snapshot with { Stage = MapStage(stage) });

    private void ApplyStreamedText(int runId, string text, bool isFinal)
        => Mutate(
            runId,
            snapshot => isFinal
                ? snapshot with
                {
                    TempResult = string.Empty,
                    FinalResult = text.Trim(),
                }
                : snapshot with { TempResult = text });

    private void RecordFailure(int runId, Exception exception, bool chatStarted)
    {
        var errorKind = exception switch
        {
            AIChatEmptyResponseException => AIConversationErrorKind.EmptyResponse,
            AIChatInitializationException => AIConversationErrorKind.InitializationFailure,
            AIArticleContentEmptyException => AIConversationErrorKind.ArticleContentEmpty,
            _ => chatStarted ? AIConversationErrorKind.ChatFailure : AIConversationErrorKind.ContextFailure,
        };
        var message = errorKind == AIConversationErrorKind.ArticleContentEmpty
            ? string.Empty
            : exception.Message;
        Mutate(runId, snapshot => snapshot with
        {
            Stage = null,
            IsGenerating = false,
            TempResult = string.Empty,
            ErrorKind = errorKind,
            ErrorMessage = message,
        });
    }

    private void Mutate(int runId, Func<AIConversationSnapshot, AIConversationSnapshot> mutate)
    {
        lock (_gate)
        {
            if (_runId != runId)
            {
                return;
            }

            _state = mutate(_state);
            PublishState(runId);
        }
    }

    private void PublishState(int runId)
    {
        // 调用方持有 _gate，状态与投递顺序保持一致。
        var snapshot = _state;
        _scheduler.Post(() =>
        {
            lock (_gate)
            {
                if (_runId != runId)
                {
                    return;
                }
            }

            StateChanged?.Invoke(this, new AIConversationStateChangedEventArgs(snapshot));
        });
    }

    private sealed class ContextStageProgress(Action<AIContextStage> report) : IProgress<AIContextStage>
    {
        public void Report(AIContextStage value) => report(value);
    }
}
