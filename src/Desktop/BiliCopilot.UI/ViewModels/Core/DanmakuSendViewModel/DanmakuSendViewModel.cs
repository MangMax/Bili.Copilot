// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.UI;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class DanmakuSendViewModel : ViewModelBase
{
    public DanmakuSendViewModel(
        IDanmakuService service,
        AccountViewModel account,
        ILogger<DanmakuSendViewModel> logger)
    {
        _danmakuService = service;
        _account = account;
        _logger = logger;
        Locations = [DanmakuLocation.Scroll, DanmakuLocation.Top, DanmakuLocation.Bottom];
        Location = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.DanmakuLocation, DanmakuLocation.Scroll);
        IsStandardSize = SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.IsDanmakuStandardSize, true);
        Color = AppToolkit.HexToColor(SettingsToolkit.ReadLocalSetting(Models.Constants.SettingNames.DanmakuColor, "FFFFFF"));
    }

    /// <summary>
    /// 重置数据.
    /// </summary>
    public void ResetData(string aid, string cid)
    {
        _aid = aid;
        _cid = cid;
    }

    public async Task<Color?> SendDanmakuAsync(string text, int position)
    {
        try
        {
            var color = CanSendColoredDanmaku ? Color : Microsoft.UI.Colors.White;
            var danmakuColor = (color.R << 16) | (color.G << 8) | color.B;
            await _danmakuService.SendVideoDanmakuAsync(text, _aid, _cid, position, danmakuColor.ToString(), IsStandardSize, Location);
            return color;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "弹幕发送失败.");
            return null;
        }
    }

    partial void OnIsStandardSizeChanged(bool value)
        => SettingsToolkit.WriteLocalSetting(Models.Constants.SettingNames.IsDanmakuStandardSize, value);

    partial void OnLocationChanged(DanmakuLocation value)
        => SettingsToolkit.WriteLocalSetting(Models.Constants.SettingNames.DanmakuLocation, value);

    partial void OnColorChanged(Windows.UI.Color value)
        => SettingsToolkit.WriteLocalSetting(Models.Constants.SettingNames.DanmakuColor, ArgbColorUtility.ToRgbHex(value));
}
