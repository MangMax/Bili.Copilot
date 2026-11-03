// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.View;
using Richasy.BiliKernel.Models.User;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Pages.Overlay;

/// <summary>
/// 用户空间页面.
/// </summary>
public sealed partial class UserSpacePage : UserSpacePageBase, IParameterPage, ICancelPageViewModel
{
    private int _loadGeneration = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSpacePage"/> class.
    /// </summary>
    public UserSpacePage() => InitializeComponent();

    public void SetParameter(object? parameter)
    {
        if (parameter is UserProfile profile)
        {
            // 叠层按类型缓存页面，同一页面会再次收到新的用户参数。
            ViewModel.InitializeCommand.Execute(profile);

            // 记录本次页面发起的加载代次，卸载时只取消这一代，不影响新页面。
            _loadGeneration = ViewModel.CurrentLoadGeneration;
        }
    }

    public void CancelLoading()
        => ViewModel.CancelLoad(_loadGeneration);

    protected override void OnPageUnloaded()
        => ViewModel.CancelLoad(_loadGeneration);
}

/// <summary>
/// 用户空间页面基类.
/// </summary>
public abstract class UserSpacePageBase : LayoutPageBase<UserSpacePageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserSpacePageBase"/> class.
    /// </summary>
    protected UserSpacePageBase()
    {
        ViewModel = this.Get<UserSpacePageViewModel>();
    }
}
