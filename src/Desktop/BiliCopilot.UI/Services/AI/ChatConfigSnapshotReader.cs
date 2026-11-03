// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.AI.Chat;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Richasy.AgentKernel;

namespace BiliCopilot.UI.Services.AI;

/// <summary>
/// 基于聊天配置管理器与本地设置的配置快照读取器.
/// </summary>
/// <remarks>
/// 每次请求前读取当前选中的服务与模型，并把服务配置和流式开关一起固化到快照中，
/// 使在途请求不受请求中途的配置切换影响.
/// </remarks>
internal sealed class ChatConfigSnapshotReader : IAIChatConfigSnapshotReader
{
    private readonly IChatConfigManager _configManager;
    private readonly Func<AIChatConfigSelection> _selectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatConfigSnapshotReader"/> class.
    /// </summary>
    /// <param name="configManager">聊天服务配置管理器.</param>
    /// <param name="selectionProvider">读取当前服务与模型的委托.</param>
    public ChatConfigSnapshotReader(IChatConfigManager configManager, Func<AIChatConfigSelection> selectionProvider)
    {
        ArgumentNullException.ThrowIfNull(configManager);
        ArgumentNullException.ThrowIfNull(selectionProvider);
        _configManager = configManager;
        _selectionProvider = selectionProvider;
    }

    /// <inheritdoc/>
    public async Task<AIChatConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selection = _selectionProvider();
        var config = await _configManager.GetServiceConfigAsync(selection.Provider, selection.Model).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var useStreaming = SettingsToolkit.ReadLocalSetting(SettingNames.IsAIStreamingResponse, true);
        return new AIChatConfigSnapshot(selection.Provider.ToString(), selection.Model.Id, config, useStreaming);
    }
}
