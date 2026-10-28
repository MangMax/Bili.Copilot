// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Forms;
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
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(MomentImageGallery), new PropertyMetadata(default));

    /// <summary>
    /// Initializes a new instance of the <see cref="MomentImageGallery"/> class.
    /// </summary>
    public MomentImageGallery() => InitializeComponent();

    /// <summary>
    /// 图片源.
    /// </summary>
    public object ItemsSource
    {
        get => (object)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnControlUnloaded()
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

        var itemsSource = ItemsSource as List<BiliImage>;
        new GalleryWindow(image, itemsSource).Activate();
    }
}
