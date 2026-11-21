// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.Specialized;

namespace BiliCopilot.UI.Controls.Users;

/// <summary>
/// 关注页面主体.
/// </summary>
public sealed partial class FollowsMainBody : FollowsPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FollowsMainBody"/> class.
    /// </summary>
    public FollowsMainBody() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        ViewModel.UserListUpdated += OnUserListUpdatedAsync;
        ViewModel.Users.CollectionChanged += OnUsersChanged;
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        ViewModel.UserListUpdated -= OnUserListUpdatedAsync;
        ViewModel.Users.CollectionChanged -= OnUsersChanged;
    }

    private void OnUsersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            View.ResetScrollPosition();
        }
    }

    private async void OnUserListUpdatedAsync(object? sender, EventArgs e)
    {
        await View.DelayCheckItemsAsync();
    }
}
