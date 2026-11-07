// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;
using Microsoft.UI.Xaml.Automation;
using System.ComponentModel;

namespace BiliCopilot.UI.Controls.Player;

/// <summary>
/// 视频播放页侧边栏头部.
/// </summary>
public sealed partial class VideoPlayerSideHeader : VideoPlayerControlBase
{
    private bool _attached;
    private VideoConnectorViewModel? _subscribedModel;

    /// <summary>Initializes a new instance of the <see cref="VideoPlayerSideHeader"/> class.</summary>
    public VideoPlayerSideHeader() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        Tabs.SelectionChanged += OnSelectorChanged;
        SubscribeToModel(ViewModel);
        InitializeChildPartitions();
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        _attached = false;
        SubscribeToModel(null);
        Tabs.SelectionChanged -= OnSelectorChanged;
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(VideoConnectorViewModel? oldValue, VideoConnectorViewModel? newValue)
    {
        SubscribeToModel(_attached ? newValue : null);
        InitializeChildPartitions();
    }

    private void SubscribeToModel(VideoConnectorViewModel? model)
    {
        if (_subscribedModel is not null)
        {
            _subscribedModel.PropertyChanged -= OnModelPropertyChanged;
        }

        _subscribedModel = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelPropertyChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(VideoConnectorViewModel.Sections))
        {
            InitializeChildPartitions();
        }
        else if (e.PropertyName == nameof(VideoConnectorViewModel.SelectedSection))
        {
            Tabs.Select(ViewModel?.SelectedSection);
        }
    }

    private void OnSelectorChanged(object? sender, EventArgs args)
    {
        if (Tabs.SelectedItem is IPlayerSectionDetailViewModel item
            && ViewModel?.Sections?.Contains(item) == true
            && !ReferenceEquals(item, ViewModel.SelectedSection))
        {
            ViewModel.SelectSectionCommand.Execute(item);
        }
    }

    private void InitializeChildPartitions()
    {
        var items = ViewModel?.Sections?.Select((section, index) =>
        {
            var item = new SelectorBarItem
            {
                Text = section.Title ?? string.Empty,
                Tag = section,
            };
            AutomationProperties.SetAutomationId(item, $"VideoPlayerSection{index}");
            AutomationProperties.SetName(item, section.Title ?? string.Empty);
            return item;
        }) ?? Enumerable.Empty<SelectorBarItem>();
        Tabs.SetItems(items, ViewModel?.SelectedSection);
    }
}
