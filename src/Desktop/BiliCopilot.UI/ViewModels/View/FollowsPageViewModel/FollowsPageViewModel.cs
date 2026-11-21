// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages.Overlay;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Models.User;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 关注页面视图模型.
/// </summary>
public sealed partial class FollowsPageViewModel : LayoutPageViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FollowsPageViewModel"/> class.
    /// </summary>
    public FollowsPageViewModel(
        IRelationshipService service,
        ILogger<FollowsPageViewModel> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override string GetPageKey()
        => nameof(FollowsPage);

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsGroupLoading || Groups.Count > 0)
        {
            return;
        }

        await LoadGroupsAsync();
        var lastSelectedGroupId = SettingsToolkit.ReadLocalSetting(SettingNames.FollowsPageLastSelectedGroupId, string.Empty);
        var group = Groups.FirstOrDefault(p => p.Id == lastSelectedGroupId) ?? Groups.FirstOrDefault(p => p.TotalCount > 0) ?? Groups.FirstOrDefault();
        SelectGroupCommand.Execute(group);
        GroupInitialized?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectGroup(UserGroup group)
    {
        if (group == null || group == SelectedGroup)
        {
            return;
        }

        CacheSelectedGroup();
        SearchKeyword = string.Empty;
        SearchText = string.Empty;
        SelectedGroup = group;
        SettingsToolkit.WriteLocalSetting(SettingNames.FollowsPageLastSelectedGroupId, group.Id);
        RestoreGroup();
    }

    [RelayCommand]
    private void Search(string? text)
    {
        var keyword = text?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            if (SearchKeyword.Length > 0)
            {
                SearchKeyword = string.Empty;
                RestoreGroup();
            }

            return;
        }

        CacheSelectedGroup();
        SearchKeyword = keyword;
        ResetUserList();
        _searchPage = 1;
        _searchHasMore = true;
        LoadUsersCommand.Execute(default);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadUsersAsync()
    {
        var isSearch = SearchKeyword.Length > 0;
        if (IsUserLoading || (isSearch ? !_searchHasMore : SelectedGroup is null || Users.Count >= SelectedGroup.TotalCount))
        {
            return;
        }

        using var load = new CancellationTokenSource();
        _listLoad = load;
        IsUserLoading = true;
        try
        {
            IReadOnlyList<UserCard> users;
            if (isSearch)
            {
                var (matches, hasMore) = await _service.SearchMyFollowingsAsync(SearchKeyword, _searchPage, load.Token);
                load.Token.ThrowIfCancellationRequested();
                users = matches;
                _searchHasMore = hasMore;
                _searchPage++;
            }
            else
            {
                var group = SelectedGroup;
                var (members, nextOffset) = await _service.GetMyFollowUserGroupDetailAsync(group.Id, _offsetCache.GetValueOrDefault(group), load.Token);
                load.Token.ThrowIfCancellationRequested();
                users = members;
                _offsetCache[group] = nextOffset;
            }

            foreach (var item in users)
            {
                Users.Add(new UserItemViewModel(item));
            }

            IsEmpty = Users.Count == 0;
            if (users.Count > 0)
            {
                UserListUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!load.IsCancellationRequested)
            {
                _logger.LogError(ex, "尝试加载关注用户时出错.");
                if (isSearch)
                {
                    _searchHasMore = false;
                    IsSearchFailed = true;
                }
            }
        }
        finally
        {
            if (_listLoad == load)
            {
                _listLoad = null;
                IsUserLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SearchKeyword.Length > 0)
        {
            Search(SearchKeyword);
            return;
        }

        if (!IsGroupLoading)
        {
            ResetUserList();
            Groups.Clear();
            _userCache.Clear();
            _offsetCache.Clear();
            SelectedGroup = default;
            await InitializeAsync();
        }
    }

    private void CacheSelectedGroup()
    {
        if (SearchKeyword.Length == 0 && SelectedGroup is { } group && Users.Count > 0)
        {
            _userCache[group] = Users.ToList();
        }
    }

    private void RestoreGroup()
    {
        ResetUserList();
        if (SelectedGroup is { } group && _userCache.TryGetValue(group, out var users))
        {
            foreach (var user in users)
            {
                Users.Add(user);
            }

            UserListUpdated?.Invoke(this, EventArgs.Empty);
        }
        else if (SelectedGroup is { TotalCount: > 0 })
        {
            LoadUsersCommand.Execute(default);
        }
        else
        {
            IsEmpty = true;
        }
    }

    private void ResetUserList()
    {
        _listLoad?.Cancel();
        _listLoad = null;
        IsUserLoading = false;
        IsSearchFailed = false;
        IsEmpty = false;
        Users.Clear();
    }

    private async Task LoadGroupsAsync()
    {
        IsGroupLoading = true;
        try
        {
            var groups = await _service.GetMyFollowUserGroupsAsync();
            if (groups != null)
            {
                foreach (var item in groups)
                {
                    Groups.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试加载关注分组时出错.");
        }
        finally
        {
            IsGroupLoading = false;
        }
    }
}
