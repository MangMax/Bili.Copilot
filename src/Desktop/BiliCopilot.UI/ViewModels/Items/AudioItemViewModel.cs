// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models.Media;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.System;

namespace BiliCopilot.UI.ViewModels.Items;

/// <summary>
/// 音频条目视图模型.
/// </summary>
public sealed partial class AudioItemViewModel : ViewModelBase<AudioInformation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AudioItemViewModel"/> class.
    /// </summary>
    /// <param name="data">音频信息.</param>
    public AudioItemViewModel(AudioInformation data)
        : base(data)
    {
        Title = data.Identifier.Title ?? string.Empty;
        Cover = data.Identifier.Cover?.Uri;
        Author = data.Author ?? string.Empty;
        Duration = AppToolkit.FormatDuration(TimeSpan.FromSeconds(data.Duration ?? 0));
        PlayCount = AppToolkit.FormatCount(data.PlayCount ?? 0);
    }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 封面.
    /// </summary>
    public Uri? Cover { get; }

    /// <summary>
    /// 作者.
    /// </summary>
    public string Author { get; }

    /// <summary>
    /// 时长.
    /// </summary>
    public string Duration { get; }

    /// <summary>
    /// 播放数.
    /// </summary>
    public string PlayCount { get; }

    /// <summary>
    /// 打开音频页面.
    /// </summary>
    /// <returns>打开任务.</returns>
    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!string.IsNullOrEmpty(Data.Identifier.Id))
        {
            await Launcher.LaunchUriAsync(new Uri($"https://www.bilibili.com/audio/au{Data.Identifier.Id}"));
        }
    }
}
