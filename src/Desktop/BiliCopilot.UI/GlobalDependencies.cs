// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Chat;
using BiliCopilot.UI.AI.Conversation;
using BiliCopilot.UI.AI.Context;
using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.Logging;
using BiliCopilot.UI.Resolvers;
using BiliCopilot.UI.Services.AI;
using BiliCopilot.UI.SponsorBlock;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.View;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.AgentKernel;
using Richasy.AgentKernel.Chat;
using Richasy.BiliKernel;
using Richasy.BiliKernel.Bili.Comment;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.User;
using Richasy.WinUIKernel.AI;
using Richasy.WinUIKernel.Share;
using Richasy.WinUIKernel.Share.Toolkits;
using Richasy.WinUIKernel.Share.ViewModels;
using RichasyKernel;
using Serilog;
using System.Diagnostics.CodeAnalysis;

namespace BiliCopilot.UI;

internal static class GlobalDependencies
{
    /// <summary>
    /// 获取服务提供程序.
    /// </summary>
    public static Kernel Kernel { get; private set; }

    public static void Initialize()
    {
        if (Kernel is not null)
        {
            return;
        }

        Kernel = Kernel.CreateBuilder()
            .AddSerilog()
            .AddDispatcherQueue()
            .AddShareToolkits()
            .AddConfigManager()
            .AddXamlRootProvider()

            .AddBiliClient()
            .AddBiliAuthenticator()
            .AddWinUICookiesResolver()
            .AddWinUITokenResolver()
            .AddWinUIQRCodeResolver(RenderQRCodeAsync)
            .AddTVAuthenticationService()
            .AddMyProfileService()
            .AddUserService()
            .AddRelationshipService()
            .AddViewLaterService()
            .AddViewHistoryService()
            .AddCommentService()
            .AddVideoDiscoveryService()
            .AddLiveDiscoveryService()
            .AddEntertainmentDiscoveryService()
            .AddArticleDiscoveryService()
            .AddArticleOperationService()
            .AddMomentDiscoveryService()
            .AddMomentOperationService()
            .AddMessageService()
            .AddFavoriteService()
            .AddSearchService()
            .AddDanmakuService()
            .AddSubtitleService()
            .AddPlayerService()
            .AddLiveDanmakuService()
            .AddSponsorBlockService()
            .AddAIConversation()

            .AddOpenAIChatService()
            .AddAzureOpenAIChatService()
            .AddAzureAIChatService()
            .AddXAIChatService()
            .AddZhiPuChatService()
            .AddLingYiChatService()
            .AddAnthropicChatService()
            .AddMoonshotChatService()
            .AddGeminiChatService()
            .AddDeepSeekChatService()
            .AddQwenChatService()
            .AddErnieChatService()
            .AddHunyuanChatService()
            .AddSparkChatService()
            .AddDoubaoChatService()
            .AddSiliconFlowChatService()
            .AddOpenRouterChatService()
            .AddTogetherAIChatService()
            .AddGroqChatService()
            .AddOllamaChatService()
            .AddMistralChatService()
            .AddPerplexityChatService()

            .AddTransient<CommentMainViewModel>()
            .AddSingleton<PinnerViewModel>()
            .AddSingleton<AppViewModel>()
            .AddSingleton<EmoteModuleViewModel>()
            .AddSingleton<NavigationViewModel>()
            .AddSingleton<StartupPageViewModel>()
            .AddSingleton<AccountViewModel>()
            .AddVideoCardServices()
            .AddSingleton<SearchBoxViewModel>()
            .AddSingleton<PopularPageViewModel>()
            .AddSingleton<MomentPageViewModel>()
            .AddSingleton<VideoPartitionPageViewModel>()
            .AddSingleton<LivePartitionPageViewModel>()
            .AddSingleton<ArticlePartitionPageViewModel>()
            .AddSingleton<AnimePageViewModel>()
            .AddSingleton<CinemaPageViewModel>()
            .AddSingleton<FansPageViewModel>()
            .AddSingleton<FollowsPageViewModel>()
            .AddSingleton<SearchPageViewModel>()
            .AddSingleton<ArticleReaderPageViewModel>()
            .AddSingleton<ViewLaterPageViewModel>()
            .AddSingleton<HistoryPageViewModel>()
            .AddSingleton<FavoritesPageViewModel>()
            .AddSingleton<MessagePageViewModel>()
            .AddSingleton<SettingsPageViewModel>()
            .AddSingleton<UserSpacePageViewModel>()
            .AddSingleton<NotificationViewModel>()
            .AddTransient<SubtitleViewModel>()
            .AddTransient<AIViewModel>()
            .AddTransient<UserMomentDetailViewModel>()
            .AddTransient<DownloadViewModel>()
            .AddTransient<VideoMediaSourceResolver>()
            .AddTransient<VideoMediaHistoryResolver>()
            .AddTransient<PgcMediaSourceResolver>()
            .AddTransient<PgcMediaHistoryResolver>()
            .AddTransient<LiveMediaSourceResolver>()
            .AddTransient<LiveMediaHistoryResolver>()
            .AddTransient<PlayerViewModel>()
            .AddTransient<DanmakuSendViewModel>()
            .AddNotificationViewModel()
            .Build();

        Kernel.InitializeShareKernel(Kernel.Get<ILogger<App>>());
        Kernel.InitializeAIKernel();
    }

    public static IKernelBuilder AddDispatcherQueue(this IKernelBuilder builder)
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        builder.Services.AddSingleton(queue);
        return builder;
    }

    public static IKernelBuilder AddNotificationViewModel(this IKernelBuilder builder)
    {
        builder.Services.AddSingleton<INotificationViewModel, NotificationViewModel>();
        return builder;
    }

    public static IKernelBuilder AddShareToolkits(this IKernelBuilder builder)
    {
        builder.Services.AddSingleton<IAppToolkit, AppToolkit>()
            .AddSingleton<ISettingsToolkit, SettingsToolkit>()
            .AddSingleton<IFileToolkit, FileToolkit>()
            .AddSingleton<IResourceToolkit, ResourceToolkit>()
            .AddSingleton<IFontToolkit, SharedFontToolkit>();
        return builder;
    }

    public static IKernelBuilder AddXamlRootProvider(this IKernelBuilder builder)
    {
        builder.Services.AddSingleton<IXamlRootProvider, XamlRootProvider>();
        return builder;
    }

    public static IKernelBuilder AddSponsorBlockService(this IKernelBuilder builder)
    {
        // 使用工厂注册，避免 DI 尝试注入可选参数中的 HttpClient.
        builder.Services.AddSingleton(_ => new SponsorBlockService());
        return builder;
    }

    public static IKernelBuilder AddConfigManager(this IKernelBuilder builder)
    {
        builder.Services
            .AddSingleton<IChatConfigManager, ChatConfigManager>();
        return builder;
    }

    public static IKernelBuilder AddAIConversation(this IKernelBuilder builder)
    {
        builder.Services.AddSingleton<AIChatSelectionAccessor>();
        builder.Services.AddSingleton<IAIChatConfigSnapshotReader>(sp => new ChatConfigSnapshotReader(
            sp.GetRequiredService<IChatConfigManager>(),
            () => sp.GetRequiredService<AIChatSelectionAccessor>().Current
                ?? throw new InvalidOperationException("尚未选择 AI 服务或模型")));
        builder.Services.AddSingleton<IAIChatModelClient>(
            _ => new AgentKernelChatModelClient(name => Kernel.GetRequiredService<IChatService>(name)));
        builder.Services.AddSingleton<IAIConversationScheduler>(
            sp => new DispatcherQueueConversationScheduler(sp.GetRequiredService<DispatcherQueue>()));
        builder.Services.AddSingleton<IAIPromptTemplateProvider>(
            sp => new FileAIPromptTemplateProvider(sp.GetRequiredService<IFileToolkit>()));
        builder.Services.AddTransient<AIContextCollector>(sp => new AIContextCollector(
            new BiliKernelSubtitleSource(sp.GetRequiredService<ISubtitleService>()),
            new BiliKernelCommentSource(sp.GetRequiredService<ICommentService>())));
        builder.Services.AddTransient<AIChatRequestRunner>(sp => new AIChatRequestRunner(
            sp.GetRequiredService<IAIChatConfigSnapshotReader>(),
            sp.GetRequiredService<IAIChatModelClient>()));
        builder.Services.AddTransient<AIConversationOrchestrator>(sp => new AIConversationOrchestrator(
            sp.GetRequiredService<AIContextCollector>(),
            sp.GetRequiredService<AIChatRequestRunner>(),
            sp.GetRequiredService<IAIConversationScheduler>()));
        return builder;
    }

    private static IKernelBuilder AddVideoCardServices(this IKernelBuilder builder)
    {
        builder.Services.AddSingleton(services => new UserRelationBatcher(
            (ids, token) => services.GetRequiredService<IRelationshipService>().GetRelationshipsAsync(ids, token),
            onError: ex => services.GetRequiredService<ILogger<UserRelationBatcher>>().LogWarning(ex, "Unable to batch publisher relationships.")));
        builder.Services.AddSingleton(services => new VideoCardMetadataProvider((identifier, token) =>
            services.GetRequiredService<IPlayerService>().GetVideoInformationAsync(identifier, token)));
        return builder;
    }

    public static IKernelBuilder AddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IKernelBuilder kernelBuilder)
        where T : class
    {
        kernelBuilder.Services.AddSingleton<T>();
        return kernelBuilder;
    }

    public static IKernelBuilder AddTransient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IKernelBuilder kernelBuilder)
        where T : class
    {
        kernelBuilder.Services.AddTransient<T>();
        return kernelBuilder;
    }

    public static RichasyKernel.IKernelBuilder AddSerilog(this RichasyKernel.IKernelBuilder builder)
    {
        var loggerPath = Path.Combine(Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.Path, "Logger");
        if (!Directory.Exists(loggerPath))
        {
            Directory.CreateDirectory(loggerPath);
        }

        AppLogLevelSwitch.Default.Apply(LogLevelSettings.ReadAppLogLevel());

        // Create a logger with current date.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(AppLogLevelSwitch.Default.LevelSwitch)
            .Filter.ByExcluding(DanmakuConfigurationLogFilter.ShouldExclude)
            .Filter.ByExcluding(logEvent => AppLogLevelFilter.ShouldExclude(logEvent, AppLogLevelSwitch.Default.Current))
            .WriteTo.File(Path.Combine(loggerPath, $"log-{DateTimeOffset.Now:yyyy-MM-dd}.txt"))
            .CreateLogger();

        builder.Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddSerilog(dispose: true));
        return builder;
    }

    public static T Get<T>(this object ele)
        where T : class
        => Kernel.GetRequiredService<T>();

    private static Task RenderQRCodeAsync(byte[] imageData)
    {
        var vm = Kernel.GetRequiredService<StartupPageViewModel>();
        vm.RenderQRCodeCommand.Execute(imageData);
        return Task.CompletedTask;
    }
}
