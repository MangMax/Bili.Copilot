// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Windows.UI.ViewManagement;

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 用户空间页面头部.
/// </summary>
public sealed partial class UserSpacePageHeader : UserSpacePageControlBase
{
    private readonly UISettings _uiSettings = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSpacePageHeader"/> class.
    /// </summary>
    public UserSpacePageHeader() => InitializeComponent();

    internal double CollapseHeight => Math.Max(0, ActualHeight - NavigationBar.ActualHeight);

    internal void ConnectScroll(ScrollView? scroll)
    {
        var header = ElementCompositionPreview.GetElementVisual(this);
        var image = ElementCompositionPreview.GetElementVisual(HeroImage);
        ElementCompositionPreview.SetIsTranslationEnabled(this, true);
        ElementCompositionPreview.SetIsTranslationEnabled(HeroImage, true);
        header.StopAnimation("Translation.Y");
        image.StopAnimation("Translation.Y");
        header.Properties.InsertVector3("Translation", Vector3.Zero);
        image.Properties.InsertVector3("Translation", Vector3.Zero);
        if (scroll is null)
        {
            return;
        }

        // 减去 MinPosition，以消除滚动锚点引入的原点偏移。
        using var collapse = header.Compositor.CreateExpressionAnimation("-Clamp(source.Position.Y - source.MinPosition.Y, 0, distance)");
        collapse.SetReferenceParameter("source", scroll.ExpressionAnimationSources);
        collapse.SetScalarParameter("distance", (float)CollapseHeight);
        header.StartAnimation("Translation.Y", collapse);
        if (_uiSettings.AnimationsEnabled)
        {
            using var parallax = header.Compositor.CreateExpressionAnimation("Clamp(source.Position.Y - source.MinPosition.Y, 0, distance) * 0.45");
            parallax.SetReferenceParameter("source", scroll.ExpressionAnimationSources);
            parallax.SetScalarParameter("distance", (float)CollapseHeight);
            image.StartAnimation("Translation.Y", parallax);
        }
    }

    public static string GetFollowLabel(bool isFollowed)
        => ResourceToolkit.GetLocalizedString(isFollowed ? StringNames.Followed : StringNames.Follow);

    private void OnHeaderSizeChanged(object sender, SizeChangedEventArgs e)
        => VisualStateManager.GoToState(this, e.NewSize.Width < 760 ? "Compact" : "Wide", false);

    private void OnHeroSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Canvas 内的图片只跟随实测尺寸，不参与页头高度计算。
        HeroImage.Width = e.NewSize.Width;
        HeroImage.Height = e.NewSize.Height;
        HeroLayout.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
    }

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        Selector.SelectionChanged += OnSelectorChanged;
        ViewModel.Initialized += OnViewModelInitialized;
        InitializeChildPartitions();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.Initialized -= OnViewModelInitialized;
        }

        Selector.SelectionChanged -= OnSelectorChanged;
    }

    private void OnViewModelInitialized(object? sender, EventArgs e)
        => InitializeChildPartitions();

    private void OnSelectorChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is null)
        {
            return;
        }

        var item = sender.SelectedItem.Tag as UserMomentDetailViewModel;
        if (item is not null && item != ViewModel.SelectedSection)
        {
            ViewModel.SelectSectionCommand.Execute(item);
        }
    }

    private void InitializeChildPartitions()
    {
        Selector.Items.Clear();
        if (ViewModel.Sections is not null)
        {
            foreach (var item in ViewModel.Sections)
            {
                Selector.Items.Add(new SelectorBarItem()
                {
                    Text = item.Title,
                    Tag = item,
                });
            }
        }

        Selector.SelectedItem = Selector.Items.FirstOrDefault(p => p.Tag == ViewModel.SelectedSection);
    }
}
