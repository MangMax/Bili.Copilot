// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models.User;
using Richasy.WinUIKernel.Share.ViewModels;
using WinRT;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 视频合作投稿人视图模型.
/// </summary>
[GeneratedBindableCustomProperty]
public sealed partial class VideoContributorViewModel : ViewModelBase<PublisherProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VideoContributorViewModel"/> class.
    /// </summary>
    /// <param name="data">合作者资料.</param>
    public VideoContributorViewModel(PublisherProfile data)
        : base(data)
    {
        Id = data.User.Id;
        Name = data.User.Name?.Trim() ?? string.Empty;
        Avatar = data.User.Avatar?.Uri;
        Role = string.IsNullOrWhiteSpace(data.Role) ? null : data.Role.Trim();
    }

    /// <summary>
    /// 用户 Id，用作列表条目的自动化标识.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 用户头像.
    /// </summary>
    public Uri? Avatar { get; }

    /// <summary>
    /// 在视频中承担的角色.
    /// </summary>
    public string? Role { get; }

    [RelayCommand]
    private void OpenUserSpace()
        => this.Get<NavigationViewModel>().NavigateToOver(typeof(UserSpacePage), Data.User);
}
