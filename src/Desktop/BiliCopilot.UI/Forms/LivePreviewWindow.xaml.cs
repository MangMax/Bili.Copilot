using BiliCopilot.UI.ViewModels.Core;
using Windows.Graphics;

namespace BiliCopilot.UI.Forms;

/// <summary>
/// 临时预览窗口：直接复用真实 PlayerOverlay，用于直播播放器布局验收（分支内，不合并）.
/// </summary>
public sealed partial class LivePreviewWindow : Window
{
    public LivePreviewWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(1280, 760));
        var player = GlobalDependencies.Kernel.GetRequiredService<PlayerViewModel>();
        Transport.ViewModel = player;
    }
}