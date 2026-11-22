// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Items;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 动态卡片操作面板.
/// </summary>
public sealed partial class MomentItemOperationPanel : MomentItemOperationPanelBase
{
    public static readonly DependencyProperty SeparatorPaddingProperty = DependencyProperty.Register(
        nameof(SeparatorPadding), typeof(Thickness), typeof(MomentItemOperationPanel), new PropertyMetadata(new Thickness(0, 12, 0, 0)));

    /// <summary>Space between the divider and its actions.</summary>
    public Thickness SeparatorPadding
    {
        get => (Thickness)GetValue(SeparatorPaddingProperty);
        set => SetValue(SeparatorPaddingProperty, value);
    }

    /// <summary>
    /// 收藏数还未取回时先显示「收藏」两个字，取回后显示格式化数量.
    /// </summary>
    /// <param name="count">收藏数.</param>
    /// <returns>用于按钮显示的文本.</returns>
    public static string FormatFavoriteCount(double? count)
        => count is null
            ? ResourceToolkit.GetLocalizedString(StringNames.Favorite)
            : AppToolkit.FormatCount(System.Convert.ToInt64(count));

    /// <summary>
    /// Initializes a new instance of the <see cref="MomentItemOperationPanel"/> class.
    /// </summary>
    public MomentItemOperationPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadFavoriteCount();
    }

    protected override void OnViewModelChanged(MomentItemViewModel? oldValue, MomentItemViewModel? newValue)
    {
        if (IsLoaded)
        {
            LoadFavoriteCount();
        }
    }

    private void LoadFavoriteCount()
    {
        if (ViewModel is { CanFavorite: true } viewModel)
        {
            _ = viewModel.LoadFavoriteCountAsync();
        }
    }

    private async void OnFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var dialog = new FavoriteFolderDialog(ViewModel, XamlRoot);
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.FavoriteCommand.Execute(default);
        }
        else
        {
            // 取消后丢弃临时勾选，下次打开重新读取服务端状态.
            ViewModel.DiscardFavoriteFolders();
        }
    }
}

/// <summary>
/// 动态卡片操作面板基类.
/// </summary>
public abstract class MomentItemOperationPanelBase : LayoutUserControlBase<MomentItemViewModel>
{
}
