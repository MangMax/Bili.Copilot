// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Humanizer;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.User;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.ViewModels;
using System.Globalization;

namespace BiliCopilot.UI.ViewModels.Components;

/// <summary>
/// 聊天消息区块详情视图模型.
/// </summary>
public sealed partial class ChatMessageSectionDetailViewModel : ViewModelBase<ChatSession>, IMessageSectionDetailViewModel
{
    private const int InitialMessageCount = 100;
    private const int InitialLoadTarget = 500;
    private const int MaxMessageCount = 2000;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessageSectionDetailViewModel"/> class.
    /// </summary>
    public ChatMessageSectionDetailViewModel(
        ChatSession session)
        : base(session)
    {
        _service = this.Get<IMessageService>();
        _logger = this.Get<ILogger<ChatMessageSectionDetailViewModel>>();

        Avatar = session.User?.Avatar?.Uri;
        UserName = session.User?.Name;
        UnreadCount = session.UnreadCount;
        LastMessage = Data.LastMessage;
        LastMessageTime = Data.Time.Humanize(default, new System.Globalization.CultureInfo("zh-CN"));
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (Messages?.Count > 0 || _preventLoadMore || IsEmpty)
        {
            return;
        }

        Messages = new();
        await LoadLatestMessagesAsync();
    }

    [RelayCommand]
    private void ShowUserSpace()
        => this.Get<NavigationViewModel>().NavigateToOver(typeof(UserSpacePage), Data.User);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Messages = new();
        _preventLoadMore = false;
        _requestedMessageCount = InitialMessageCount;
        HasMoreMessages = false;
        IsEmpty = false;
        await LoadLatestMessagesAsync();
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrEmpty(InputText) || IsSending)
        {
            return;
        }

        try
        {
            IsSending = true;
            var now = DateTimeOffset.Now;
            var msg = await _service.SendChatMessageAsync(InputText, Data.User);
            if (msg is not null)
            {
                Messages ??= new();
                if (!Messages.Any(p => p.Data.Key == msg.Key))
                {
                    Messages.Add(new ChatMessageItemViewModel(msg));
                }

                InputText = string.Empty;
                LastMessage = msg.Content?.Text ?? LastMessage;
                LastMessageTime = now.Humanize(default, new CultureInfo("zh-CN"));
                RequestScrollToBottom?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息时失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToSendMessage), InfoType.Error));
        }
        finally
        {
            IsSending = false;
            IsEmpty = Messages.Count == 0;
        }
    }

    [RelayCommand]
    private async Task LoadOlderMessagesAsync()
    {
        if (IsLoading || IsLoadingOlder || !HasMoreMessages || _preventLoadMore)
        {
            return;
        }

        if (Messages is null || Messages.Count == 0)
        {
            HasMoreMessages = false;
            return;
        }

        try
        {
            IsLoadingOlder = true;

            // 接口只接受“最近 N 条”，没有偏移量，因此通过逐步放大 N 来获取更早的消息.
            var next = (int)Math.Min(MaxMessageCount, Math.Max((long)_requestedMessageCount * 2, (long)_requestedMessageCount + InitialMessageCount));
            if (next <= _requestedMessageCount)
            {
                HasMoreMessages = false;
                return;
            }

            var (messages, _, hasMore) = await _service.GetChatMessagesAsync(Data.User, next);
            _requestedMessageCount = next;
            if (messages is null || messages.Count == 0)
            {
                HasMoreMessages = false;
                return;
            }

            var currentMessages = Messages.Select(p => p.Data).ToList();
            var mergeResult = ChatMessageMerger.Merge(currentMessages, messages, p => p.Key);
            if (mergeResult.AddedItems.Count == 0)
            {
                // 服务端限制了单次返回数量，继续放大请求也不会得到更早的消息.
                HasMoreMessages = false;
                return;
            }

            var minKey = currentMessages.Min(p => p.Key);
            var maxKey = currentMessages.Max(p => p.Key);
            if (mergeResult.AddedItems.All(p => p.Key < minKey))
            {
                var older = mergeResult.AddedItems.Select(p => new ChatMessageItemViewModel(p)).ToList();
                for (var i = older.Count - 1; i >= 0; i--)
                {
                    Messages.Insert(0, older[i]);
                }
            }
            else if (mergeResult.AddedItems.All(p => p.Key > maxKey))
            {
                foreach (var item in mergeResult.AddedItems)
                {
                    Messages.Add(new ChatMessageItemViewModel(item));
                }

                UpdateLatestMessage(mergeResult.Items);
                RequestScrollToBottom?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // 新旧消息交错（例如发送后重新取页），整体重建一次以保证顺序.
                ReplaceMessages(mergeResult.Items);
            }

            HasMoreMessages = hasMore && next < MaxMessageCount;
        }
        catch (Exception ex)
        {
            HasMoreMessages = false;
            _logger.LogError(ex, "加载更早的聊天消息时失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToLoadChatMessages), InfoType.Error));
        }
        finally
        {
            IsLoadingOlder = false;
        }
    }

    private async Task LoadLatestMessagesAsync()
    {
        if (IsLoading || _preventLoadMore)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var merged = new List<ChatMessage>();
            var requested = InitialMessageCount;
            var previousCount = 0;
            var hasMore = false;

            while (true)
            {
                var (messages, _, pageHasMore) = await _service.GetChatMessagesAsync(Data.User, requested);
                if (messages is null)
                {
                    break;
                }

                var result = ChatMessageMerger.Merge(merged, messages, p => p.Key);
                merged = result.Items.ToList();
                hasMore = pageHasMore;

                if (!hasMore || merged.Count >= InitialLoadTarget)
                {
                    break;
                }

                // 服务端可能限制单次返回数量：请求更大但消息数不再增长时停止，避免无限循环.
                if (merged.Count <= previousCount)
                {
                    hasMore = false;
                    break;
                }

                previousCount = merged.Count;
                var next = Math.Min(MaxMessageCount, Math.Max(requested * 2, merged.Count + InitialMessageCount));
                if (next <= requested)
                {
                    hasMore = false;
                    break;
                }

                requested = next;
            }

            _requestedMessageCount = requested;
            HasMoreMessages = hasMore && requested < MaxMessageCount;
            ReplaceMessages(merged);
            UpdateLatestMessage(merged);
            if (merged.Count > 0)
            {
                RequestScrollToBottom?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _preventLoadMore = true;
            _logger.LogError(ex, "加载聊天消息时失败");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.FailedToLoadChatMessages), InfoType.Error));
        }
        finally
        {
            IsLoading = false;
            IsEmpty = Messages?.Count == 0;
        }
    }

    private void ReplaceMessages(IReadOnlyList<ChatMessage> messages)
    {
        Messages ??= new();
        Messages.Clear();
        foreach (var item in messages)
        {
            Messages.Add(new ChatMessageItemViewModel(item));
        }
    }

    private void UpdateLatestMessage(IReadOnlyList<ChatMessage> messages)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var latest = messages[^1];
        var text = latest.Content?.Text ?? string.Empty;
        if (LastMessage != text)
        {
            LastMessage = text;
            if (latest.Time is not null)
            {
                LastMessageTime = latest.Time.Value.Humanize(default, new CultureInfo("zh-CN"));
            }
        }
    }
}
