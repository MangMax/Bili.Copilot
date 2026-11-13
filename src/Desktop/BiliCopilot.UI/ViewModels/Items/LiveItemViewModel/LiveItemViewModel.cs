// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using Humanizer;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.System;
using WinRT;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 直播条目视图模型.
/// </summary>
[GeneratedBindableCustomProperty]
public sealed partial class LiveItemViewModel : ViewModelBase<LiveInformation>
{
    private readonly Action<LiveItemViewModel>? _removeAction;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveItemViewModel"/> class.
    /// </summary>
    public LiveItemViewModel(LiveInformation data, LiveCardStyle style, Action<LiveItemViewModel>? removeAction = default)
        : base(data)
    {
        Title = HtmlText.Decode(data.Identifier.Title);
        Cover = data.Identifier.Cover?.Uri;
        Author = data.User?.Name;
        Avatar = data.User?.Avatar?.Uri;
        Style = style;
        ViewerCount = data.GetExtensionIfNotNull<double>(LiveExtensionDataId.ViewerCount);
        Subtitle = data.GetExtensionIfNotNull<string>(VideoExtensionDataId.Subtitle);
        TagName = data.GetExtensionIfNotNull<string>(LiveExtensionDataId.TagName);
        var collectTime = data.GetExtensionIfNotNull<DateTimeOffset?>(LiveExtensionDataId.CollectTime);
        if (collectTime is not null)
        {
            CollectRelativeTime = collectTime.Value.Humanize();
        }

        _removeAction = removeAction;
    }

    /// <summary>
    /// 左键打开直播间：默认进入应用内播放器；"直播时使用网页播放器"开启时仍走浏览器。
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private async Task PlayAsync()
    {
        var useWebPlayer = SettingsToolkit.ReadLocalSetting(SettingNames.UseWebPlayerWhenLive, false);
        if (LiveEntryPolicy.Decide(useWebPlayer) == LiveEntryTarget.Browser)
        {
            await OpenInBrowserAsync();
            return;
        }

        this.Get<AppViewModel>().OpenPlayerCommand.Execute(new MediaSnapshot(Data));
        await Task.CompletedTask;
    }

    /// <summary>
    /// 在系统浏览器打开直播间；右键菜单与原生播放失败兜底共用。
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private Task OpenInBrowserAsync()
    {
        var url = LiveRoomUrl.Create(Data.Identifier.Id);
        return url is null ? Task.CompletedTask : Launcher.LaunchUriAsync(url).AsTask();
    }

    [RelayCommand]
    private async Task RemoveHistoryAsync()
    {
        try
        {
            await this.Get<IViewHistoryService>().RemoveLiveHistoryItemAsync(Data);
            _removeAction?.Invoke(this);
        }
        catch (Exception ex)
        {
            this.Get<ILogger<LiveItemViewModel>>().LogError(ex, "移除直播历史记录失败.");
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.FailedToRemoveVideoFromHistory), InfoType.Error));
        }
    }

    [RelayCommand]
    private void ShowUserSpace()
        => this.Get<NavigationViewModel>().NavigateToOver(typeof(UserSpacePage), Data.User);

    [RelayCommand]
    private void Pin()
    {
        var pinItem = new PinItem(Data.Identifier.Id, Data.Identifier.Title, Data.Identifier.Cover.Uri.ToString(), PinContentType.Live);
        this.Get<PinnerViewModel>().AddItemCommand.Execute(pinItem);
    }

}
