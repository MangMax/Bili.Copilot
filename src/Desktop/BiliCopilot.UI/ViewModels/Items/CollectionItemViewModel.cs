// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models.User;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.System;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 合集条目视图模型.
/// </summary>
public sealed partial class CollectionItemViewModel : ViewModelBase<UserCollectionInfo>
{
    private readonly string _userId;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionItemViewModel"/> class.
    /// </summary>
    /// <param name="data">合集信息.</param>
    /// <param name="userId">所属用户 Id.</param>
    public CollectionItemViewModel(UserCollectionInfo data, string userId)
        : base(data)
    {
        _userId = userId;
        Title = data.Title ?? string.Empty;
        Cover = data.Cover?.Uri;
        IsSeason = data.IsSeason;
        Count = data.Count is > 0
            ? string.Format(ResourceToolkit.GetLocalizedString(StringNames.CollectionCountTemplate), AppToolkit.FormatCount(data.Count.Value))
            : string.Empty;
    }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 封面.
    /// </summary>
    public Uri? Cover { get; }

    /// <summary>
    /// 是否为合集；否则为系列.
    /// </summary>
    public bool IsSeason { get; }

    /// <summary>
    /// 内容数量.
    /// </summary>
    public string Count { get; }

    /// <summary>
    /// 打开合集页面.
    /// </summary>
    /// <returns>打开任务.</returns>
    [RelayCommand]
    private async Task OpenAsync()
    {
        if (string.IsNullOrEmpty(Data.Id))
        {
            return;
        }

        var kind = Data.IsSeason ? "collectiondetail" : "seriesdetail";
        await Launcher.LaunchUriAsync(new Uri($"https://space.bilibili.com/{_userId}/channel/{kind}?sid={Data.Id}"));
    }
}
