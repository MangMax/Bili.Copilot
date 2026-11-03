// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 架构守卫：编排逻辑不得回流到视图模型，核心层不得依赖 WinUI 与内核，且不引入 Agent 框架。
/// </summary>
public class AIArchitectureTests
{
    private static readonly string[] ForbiddenViewModelApis =
    [
        "GetSubtitleMetasAsync",
        "GetSubtitleDetailAsync",
        "GetCommentsAsync",
        "GetStreamingResponseAsync",
        "GetServiceConfigAsync",
        "SendMessageInternalAsync",
        "BeginGeneration",
        "EnsureCurrentGeneration",
        "IsCurrentGeneration",
        "GetVideoSubtitleAsync",
        "GetHotCommentTextAsync",
        "GetArticlePlainText",
        "Regex.Replace",
        ".Replace(\"{\"",
        "StreamingResponseSession",
    ];

    private static readonly string[] ForbiddenConversationDependencies =
    [
        "Microsoft.UI",
        "DispatcherQueue",
        "ResourceToolkit",
        "IFileToolkit",
        "IChatConfigManager",
        "AgentKernel",
        "Richasy.",
        "Microsoft.Windows",
    ];

    private static readonly string[] ForbiddenPackages =
    [
        "SemanticKernel",
        "Microsoft.Agents",
        "AgentFramework",
        "Microsoft.Extensions.AI.Agents",
    ];

    [Fact]
    public void AIViewModelSources_DoNotContainOrchestrationApis()
    {
        var viewModelDirectory = Path.Combine(DesktopDirectory(), "ViewModels", "Core", "AIViewModel");
        var files = Directory.GetFiles(viewModelDirectory, "AIViewModel*.cs");

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var api in ForbiddenViewModelApis)
            {
                Assert.False(
                    text.Contains(api, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 不应再包含编排 API：{api}");
            }
        }
    }

    [Fact]
    public void ConversationCore_DoesNotDependOnWinUIOrKernel()
    {
        var directory = Path.Combine(DesktopDirectory(), "AI", "Conversation");
        var files = Directory.GetFiles(directory, "*.cs");

        Assert.Contains(files, p => Path.GetFileName(p) == "AIConversationOrchestrator.cs");
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var dependency in ForbiddenConversationDependencies)
            {
                Assert.False(
                    text.Contains(dependency, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} 不应依赖 WinUI/内核类型：{dependency}");
            }
        }
    }

    [Fact]
    public void UIProject_DoesNotReferenceAgentFrameworks()
    {
        var project = Directory.GetFiles(DesktopDirectory(), "*.UI.csproj").Single();
        var text = File.ReadAllText(project);

        foreach (var package in ForbiddenPackages)
        {
            Assert.False(
                text.Contains(package, StringComparison.OrdinalIgnoreCase),
                $"{Path.GetFileName(project)} 不应引用 Agent 框架包：{package}");
        }
    }

    private static string DesktopDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Desktop");
            if (Directory.Exists(candidate))
            {
                return Directory.GetDirectories(candidate, "*.UI").Single();
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("找不到仓库的 src/Desktop 目录。");
    }
}
