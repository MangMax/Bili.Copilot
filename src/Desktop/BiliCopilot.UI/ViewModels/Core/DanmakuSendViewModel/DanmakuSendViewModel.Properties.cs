// Copyright (c) Bili Copilot. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models;
using System.Collections.ObjectModel;
using Windows.UI;

namespace BiliCopilot.UI.ViewModels.Core;

public sealed partial class DanmakuSendViewModel
{
    private readonly IDanmakuService _danmakuService;
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

    public ObservableCollection<Color> Colors { get; } = [];
}
