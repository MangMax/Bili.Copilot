// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Microsoft.Extensions.Logging;
using Richasy.WinUIKernel.Share.Toolkits;

namespace BiliCopilot.UI.Controls.Danmaku;

/// <summary>
/// 弹幕显示选项.
/// </summary>
public sealed partial class DanmakuDisplayOptions : DanmakuControlBase
{
    public DanmakuDisplayOptions() => InitializeComponent();

    protected override async void OnControlLoaded()
    {
        var renderer = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.DanmakuRenderer, DanmakuRendererType.Win2D);
        RendererComboBox.SelectedIndex = renderer == DanmakuRendererType.DirectX ? 0 : 1;
        try
        {
            await ViewModel.ReloadFontsCommand.ExecuteAsync(default);
            if (IsLoaded)
            {
                DanmakuFontComboBox.SelectedItem = ViewModel.Fonts.FirstOrDefault(p => p.LocalName == ViewModel.DanmakuFontFamily);
            }
        }
        catch (Exception ex)
        {
            this.Get<ILogger<DanmakuDisplayOptions>>().LogWarning(ex, "Unable to load danmaku font choices");
        }
    }

    private void OnRendererChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = RendererComboBox.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        var renderer = index == 0 ? DanmakuRendererType.DirectX : DanmakuRendererType.Win2D;
        if (ViewModel.Renderer != renderer)
        {
            ViewModel.Renderer = renderer;
        }
    }

    private void OnDanmakuFontChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = DanmakuFontComboBox.SelectedItem as SystemFont;
        if (item != null && ViewModel.DanmakuFontFamily != item.LocalName)
        {
            ViewModel.DanmakuFontFamily = item.LocalName;
        }
    }
}
