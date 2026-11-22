// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// 收藏夹选择弹窗：居中显示，支持按名称搜索.
/// </summary>
public sealed partial class FavoriteFolderDialog : ContentDialog
{
    /// <summary>
    /// <see cref="ViewModel"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MomentItemViewModel), typeof(FavoriteFolderDialog), new PropertyMetadata(default));

    /// <summary>
    /// Initializes a new instance of the <see cref="FavoriteFolderDialog"/> class.
    /// </summary>
    /// <param name="viewModel">拥有收藏夹列表的视图模型.</param>
    /// <param name="xamlRoot">显示弹窗所用的 XamlRoot.</param>
    public FavoriteFolderDialog(MomentItemViewModel viewModel, XamlRoot xamlRoot)
    {
        InitializeComponent();
        ViewModel = viewModel;
        XamlRoot = xamlRoot;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// 收藏夹所属的视图模型.
    /// </summary>
    public MomentItemViewModel ViewModel
    {
        get => (MomentItemViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 每次打开都把焦点交给搜索框，列表较长时可以直接过滤.
        SearchBox.Focus(FocusState.Programmatic);
        _ = ViewModel.InitializeFavoritesCommand.ExecuteAsync(default);
    }
}
