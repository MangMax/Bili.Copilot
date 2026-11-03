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
        => CancelPageLoad();

    protected override void OnPageUnloaded()
        => CancelPageLoad();

    private void CancelPageLoad()
    {
        // 只有真正取消了本页记录的那一代，才把记录刷新为新的当前代次：
        // 返回页面后再次切走/关闭仍能取消新发起的加载，而旧页面的过期记录
        // 不会误伤之后发起的新加载。
        if (ViewModel.CancelLoad(_loadGeneration))
        {
            _loadGeneration = ViewModel.CurrentLoadGeneration;
        }
    }
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
