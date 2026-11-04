// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Conversation;
using BiliCopilot.UI.Services.AI;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.AI.ViewModels;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// AI 视图模型.
/// </summary>
public sealed partial class AIViewModel
{
    private readonly ILogger<AIViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly AIConversationOrchestrator _orchestrator;
    private readonly IAIPromptTemplateProvider _promptTemplates;
    private readonly AIChatSelectionAccessor _selectionAccessor;

    private VideoPlayerView? _videoView;
    private VideoPart? _videoPart;
    private ArticleDetail? _articleDetail;

    private AIQuickItemViewModel? _currentPrompt;
    private string? _lastQuestion;
    private AIConversationErrorKind _notifiedErrorKind;

    [ObservableProperty]
    public partial List<ChatServiceItemViewModel> Services { get; set; }

    [ObservableProperty]
    public partial List<ChatModelItemViewModel> Models { get; set; }

    [ObservableProperty]
    public partial List<AIQuickItemViewModel> QuickItems { get; set; }

    [ObservableProperty]
    public partial List<AIQuickItemViewModel> MorePrompts { get; set; }

    [ObservableProperty]
    public partial ChatServiceItemViewModel SelectedService { get; set; }

    [ObservableProperty]
    public partial ChatModelItemViewModel SelectedModel { get; set; }

    [ObservableProperty]
    public partial bool IsNoService { get; set; }

    [ObservableProperty]
    public partial bool IsNoModel { get; set; }

    [ObservableProperty]
    public partial bool IsQuickItemsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    public partial string RequestText { get; set; }

    [ObservableProperty]
    public partial string TempResult { get; set; }

    [ObservableProperty]
    public partial string FinalResult { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string ProgressTip { get; set; }

    [ObservableProperty]
    public partial string? SourceTitle { get; set; }

    [ObservableProperty]
    public partial string? SourceSubtitle { get; set; }

    [ObservableProperty]
    public partial Uri? SourceCover { get; set; }
}
