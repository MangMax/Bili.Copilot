// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using Windows.UI;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class DanmakuSendViewModel
{
    private static readonly Color[] PresetColors =
    [
        Color.FromArgb(255, 0xFE, 0x03, 0x02),
        Color.FromArgb(255, 0xFF, 0x72, 0x04),
        Color.FromArgb(255, 0xFF, 0xAA, 0x02),
        Color.FromArgb(255, 0xFF, 0xD3, 0x02),
        Color.FromArgb(255, 0xFF, 0xFF, 0x00),
        Color.FromArgb(255, 0xA0, 0xEE, 0x00),
        Color.FromArgb(255, 0x00, 0xCD, 0x00),
        Color.FromArgb(255, 0x01, 0x98, 0x99),
        Color.FromArgb(255, 0x42, 0x66, 0xBE),
        Color.FromArgb(255, 0x89, 0xD5, 0xFF),
        Color.FromArgb(255, 0xCC, 0x02, 0x73),
        Color.FromArgb(255, 0x22, 0x22, 0x22),
        Color.FromArgb(255, 0x9B, 0x9B, 0x9B),
        Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
    ];

    private readonly IDanmakuService _danmakuService;
    private readonly AccountViewModel _account;
    private readonly ILogger<DanmakuSendViewModel> _logger;

    private string _aid;
    private string _cid;

    [ObservableProperty]
    public partial bool IsStandardSize { get; set; }

    [ObservableProperty]
    public partial DanmakuLocation Location { get; set; }

    [ObservableProperty]
    public partial Color Color { get; set; }

    [ObservableProperty]
    public partial List<DanmakuLocation> Locations { get; set; }

    public IReadOnlyList<Color> Colors => PresetColors;

    public bool CanSendColoredDanmaku => _account.MyProfile?.Level >= 2;
}
