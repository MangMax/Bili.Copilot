// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.User;
using System.Collections.ObjectModel;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 聊天消息区块详情视图模型.
/// </summary>
public sealed partial class ChatMessageSectionDetailViewModel
{
    private readonly IMessageService _service;
    private readonly ILogger<ChatMessageSectionDetailViewModel> _logger;

    private bool _preventLoadMore;
    private int _requestedMessageCount = InitialMessageCount;

    [ObservableProperty]
    public partial string LastMessage { get; set; }

    [ObservableProperty]
    public partial string LastMessageTime { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingOlder { get; set; }

    [ObservableProperty]
    public partial bool HasMoreMessages { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial int UnreadCount { get; set; }

    [ObservableProperty]
    public partial bool IsSending { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; }

    /// <summary>
    /// 消息列表.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<ChatMessageItemViewModel> Messages { get; set; } = new();

    /// <summary>
    /// 请求滚动到底部.
    /// </summary>
    public event EventHandler RequestScrollToBottom;

    /// <summary>
    /// 头像.
    /// </summary>
    public Uri? Avatar { get; init; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; }
}
