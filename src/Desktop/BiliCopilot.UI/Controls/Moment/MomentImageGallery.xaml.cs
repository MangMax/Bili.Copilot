// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
using Microsoft.UI.Xaml.Media;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 动态图片画廊.
/// </summary>
public sealed partial class MomentImageGallery : LayoutUserControlBase
{
    /// <summary>
    /// <see cref="ItemsSource"/> 的依赖属性.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IReadOnlyList<BiliImage>), typeof(MomentImageGallery), new PropertyMetadata(default, OnItemsSourceChanged));

    /// <summary>
    /// Initializes a new instance of the <see cref="MomentImageGallery"/> class.
    /// </summary>
    public MomentImageGallery()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 图片源.
    /// </summary>
    public IReadOnlyList<BiliImage>? ItemsSource
    {
        get => (IReadOnlyList<BiliImage>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ImageRepeater.ItemsSource = ItemsSource;
        UpdateSingleImageRatio();
    }

    private static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((MomentImageGallery)sender).UpdateSingleImageRatio();

    private void UpdateSingleImageRatio()
    {
        if (ImageLayout is not null)
        {
            ImageLayout.SingleImageRatio = ItemsSource is [var image] && image.Width > 0 && image.Height > 0
                ? image.Width / image.Height : 1;
        }
    }

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is Button { Content: RecommendationCoverImage image })
        {
            image.Stretch = ItemsSource is { Count: 1 } ? Stretch.Uniform : Stretch.UniformToFill;
        }
    }

    private void OnImageOpened(object? sender, EventArgs args)
    {
        if (ItemsSource is [var source] && (source.Width <= 0 || source.Height <= 0) && sender is RecommendationCoverImage { SourcePixelWidth: > 0, SourcePixelHeight: > 0 } image)
            ImageLayout.SingleImageRatio = (double)image.SourcePixelWidth / image.SourcePixelHeight;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
        => ImageRepeater.ItemsSource = default;

    /// <summary>
    /// 生成图片按钮的无障碍名称，使用文件名区分同一组图片.
    /// </summary>
    /// <param name="uri">图片地址.</param>
    /// <returns>无障碍名称.</returns>
    public static string GetImageAutomationName(Uri? uri)
    {
        var name = ResourceToolkit.GetLocalizedString(StringNames.OpenImage);
        var fileName = uri is null ? null : Path.GetFileName(uri.LocalPath);
        return string.IsNullOrEmpty(fileName) ? name : $"{name} {fileName}";
    }

    /// <summary>
    /// 生成图片按钮的无障碍标识.
    /// </summary>
    /// <param name="uri">图片地址.</param>
    /// <returns>无障碍标识.</returns>
    public static string GetImageAutomationId(Uri? uri)
        => uri?.AbsoluteUri ?? "MomentImage";

    private void OnImageClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var image = (sender as FrameworkElement)?.Tag as BiliImage;
        if (image is null)
        {
            return;
        }

        var itemsSource = ItemsSource?.ToList();
        new GalleryWindow(image, itemsSource).Activate();
    }
}
