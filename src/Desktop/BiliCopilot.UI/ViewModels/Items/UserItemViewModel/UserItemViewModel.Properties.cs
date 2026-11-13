// Copyright (c) Bili Copilot. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 用户项视图模型.
/// </summary>
public sealed partial class UserItemViewModel
{
    [ObservableProperty]
    public partial bool IsFollowed { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// 用户描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 用户头像.
    /// </summary>
    public Uri? Avatar { get; init; }

    /// <summary>
    /// 是否为大会员.
    /// </summary>
    public bool IsVip { get; init; }

    /// <summary>
    /// 等级.
    /// </summary>
    public int? Level { get; init; }

    /// <summary>
    /// 用户 Id.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// 用于头像串联的用户资料，与导航命令使用同一数据源.
    /// </summary>
    public UserProfile? SpaceUser => Data.Profile.User;
}
